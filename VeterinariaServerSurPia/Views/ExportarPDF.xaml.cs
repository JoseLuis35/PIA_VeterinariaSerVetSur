using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using VeterinariaServerSurPia;

namespace VeterinariaServerSurPia.Views
{
    public sealed class MascotaParaPDF
    {
        public string ID { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string Especie { get; set; } = "";
        public string Raza { get; set; } = "";
        public string Edad { get; set; } = "";
        public string Sexo { get; set; } = "";
        public string Peso { get; set; } = "";
        public string IDCliente { get; set; } = "";
        public string Propietario { get; set; } = "";

        public string TextoMostrar => $"{ID} - {Nombre}";
    }

    public sealed partial class ExportarPDF : Page
    {
        private readonly List<string[]> clientes = new();
        private readonly List<string[]> mascotas = new();
        private readonly List<string[]> citas = new();
        private readonly List<string[]> historial = new();
        private readonly List<string[]> cartilla = new();

        private readonly HashSet<string> clientesInactivos =
            new(StringComparer.OrdinalIgnoreCase);

        static ExportarPDF()
        {
            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
        }

        public ExportarPDF()
        {
            InitializeComponent();
            CargarDatos();
            CargarSelectorMascotas();
        }

        private static string Ruta(string archivo) =>
            Path.Combine(AppContext.BaseDirectory, archivo);

        private static List<string[]> LeerArchivo(
            string archivo, char separador)
        {
            string ruta = Ruta(archivo);

            if (!File.Exists(ruta))
                return new List<string[]>();

            return File.ReadAllLines(ruta)
                .Where(linea => !string.IsNullOrWhiteSpace(linea))
                .Select(linea => linea.Split(separador))
                .ToList();
        }

        private void CargarDatos()
        {
            clientes.Clear();
            mascotas.Clear();
            citas.Clear();
            historial.Clear();
            cartilla.Clear();
            clientesInactivos.Clear();

            clientes.AddRange(LeerArchivo("clientes.txt", ','));
            mascotas.AddRange(LeerArchivo("mascotas.txt", ','));
            citas.AddRange(LeerArchivo("citas.txt", ','));
            historial.AddRange(LeerArchivo("historial_medico.txt", '|'));
            cartilla.AddRange(LeerArchivo("cartilla_medica.txt", '|'));

            string rutaInactivos = Ruta("clientes_inactivos.txt");

            if (File.Exists(rutaInactivos))
            {
                foreach (string linea in File.ReadAllLines(rutaInactivos))
                {
                    if (!string.IsNullOrWhiteSpace(linea))
                        clientesInactivos.Add(linea.Trim());
                }
            }
        }

        private void CargarSelectorMascotas()
        {
            var lista = mascotas
                .Where(p => p.Length >= 8)
                .Select(p => new MascotaParaPDF
                {
                    ID = p[0].Trim(),
                    Nombre = p[1].Trim(),
                    Especie = p[2].Trim(),
                    Raza = p[3].Trim(),
                    Edad = p[4].Trim(),
                    Sexo = p[5].Trim(),
                    Peso = p[6].Trim(),
                    IDCliente = p[7].Trim(),
                    Propietario = ObtenerNombreCliente(p[7].Trim())
                })
                .OrderBy(m => m.Nombre)
                .ToList();

            MascotaCombo.ItemsSource = lista;

            if (lista.Count == 0)
                TxtEstado.Text = "No hay mascotas registradas para exportar.";
        }

        private string ObtenerNombreCliente(string idCliente)
        {
            string[]? cliente = clientes.FirstOrDefault(p =>
                p.Length >= 2 &&
                p[0].Trim().Equals(
                    idCliente, StringComparison.OrdinalIgnoreCase));

            return cliente == null
                ? "Propietario no encontrado"
                : cliente[1].Trim();
        }

        private string ObtenerEstadoCliente(string idCliente)
        {
            return clientesInactivos.Contains(idCliente)
                ? "Inactivo (dado de baja)"
                : "Activo";
        }

        private List<string[]> ObtenerCitasMascota(string idMascota)
        {
            return citas
                .Where(p => p.Length >= 5 &&
                    p[1].Trim().Equals(
                        idMascota, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p[2])
                .ThenBy(p => p[3])
                .ToList();
        }

        private List<string[]> ObtenerHistorialMascota(string idMascota)
        {
            return historial
                .Where(p => p.Length >= 11 &&
                    p[1].Trim().Equals(
                        idMascota, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(p => p[2])
                .ToList();
        }

        private List<string[]> ObtenerCartillaMascota(string idMascota)
        {
            return cartilla
                .Where(p => p.Length >= 7 &&
                    p[1].Trim().Equals(
                        idMascota, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(p => p[4])
                .ToList();
        }

        private async void BtnExportarTodo_Click(
            object sender, RoutedEventArgs e)
        {
            if (!HaySeccionesSeleccionadas())
            {
                await MostrarAviso(
                    "Selecciona al menos una sección para exportar.");
                return;
            }

            await ExportarPDFAsync(null);
        }

        private async void BtnExportarExpediente_Click(
            object sender, RoutedEventArgs e)
        {
            if (MascotaCombo.SelectedItem is not MascotaParaPDF mascota)
            {
                await MostrarAviso(
                    "Selecciona una mascota antes de exportar su expediente.");
                return;
            }

            await ExportarPDFAsync(mascota);
        }

        private bool HaySeccionesSeleccionadas()
        {
            return ChkClientes.IsChecked == true ||
                   ChkMascotas.IsChecked == true ||
                   ChkCitas.IsChecked == true ||
                   ChkHistorial.IsChecked == true ||
                   ChkCartilla.IsChecked == true;
        }

        private async Task ExportarPDFAsync(
            MascotaParaPDF? mascotaIndividual)
        {
            try
            {
                CargarDatos();

                var picker = new FileSavePicker
                {
                    SuggestedStartLocation =
                        PickerLocationId.DocumentsLibrary,

                    SuggestedFileName = mascotaIndividual == null
                        ? $"Reporte_Veterinaria_{DateTime.Now:yyyyMMdd_HHmm}"
                        : $"Expediente_{LimpiarNombre(mascotaIndividual.Nombre)}_{DateTime.Now:yyyyMMdd}"
                };

                picker.FileTypeChoices.Add(
                    "Documento PDF",
                    new List<string> { ".pdf" });

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(
                    MainWindow.Current);

                WinRT.Interop.InitializeWithWindow.Initialize(
                    picker, hwnd);

                StorageFile? archivo = await picker.PickSaveFileAsync();

                if (archivo == null)
                {
                    TxtEstado.Text = "Exportación cancelada.";
                    return;
                }

                using (var documento = new PdfDocument())
                {
                    documento.Info.Title = mascotaIndividual == null
                        ? "Reporte general de SERVETSUR"
                        : $"Expediente médico de {mascotaIndividual.Nombre}";

                    documento.Info.Author = "SERVETSUR";

                    var escritor = new EscritorPDF(documento);

                    if (mascotaIndividual == null)
                    {
                        escritor.NuevaPagina();
                        escritor.Encabezado("REPORTE GENERAL");

                        if (ChkClientes.IsChecked == true)
                            ExportarClientes(escritor);

                        if (ChkMascotas.IsChecked == true)
                            ExportarMascotas(escritor);

                        if (ChkCitas.IsChecked == true)
                            ExportarCitas(escritor);

                        if (ChkHistorial.IsChecked == true)
                            ExportarHistorialGeneral(escritor);

                        if (ChkCartilla.IsChecked == true)
                            ExportarCartillaGeneral(escritor);
                    }
                    else
                    {
                        escritor.NuevaPagina();
                        escritor.Encabezado("EXPEDIENTE MÉDICO");
                        ExportarIdentificacion(escritor, mascotaIndividual);
                        ExportarExpedienteMascota(escritor, mascotaIndividual);
                    }

                    escritor.Finalizar();
                    documento.Save(archivo.Path);
                }

                TxtEstado.Text =
                    $"PDF creado correctamente: {archivo.Name}";

                await MostrarAviso(
                    $"El PDF se guardó correctamente en:\n{archivo.Path}");
            }
            catch (Exception ex)
            {
                TxtEstado.Text = "No se pudo generar el PDF.";

                await MostrarAviso(
                    "Ocurrió un error al generar el PDF:\n" + ex.Message);
            }
        }

        private void ExportarIdentificacion(
            EscritorPDF pdf, MascotaParaPDF mascota)
        {
            pdf.Subtitulo("DATOS DE LA MASCOTA");
            pdf.Texto($"Nombre: {mascota.Nombre}");
            pdf.Texto($"Número de expediente: {mascota.ID}");
            pdf.Texto($"Especie: {mascota.Especie}");
            pdf.Texto($"Raza: {mascota.Raza}");
            pdf.Texto($"Edad: {mascota.Edad}");
            pdf.Texto($"Sexo: {mascota.Sexo}");
            pdf.Texto($"Peso actual registrado: {mascota.Peso} kg");

            pdf.EspacioPequeno();
            pdf.Subtitulo("DATOS DEL PROPIETARIO");
            pdf.Texto($"Nombre: {mascota.Propietario}");
            pdf.Texto($"ID del propietario: {mascota.IDCliente}");
            pdf.Texto(
                $"Estado: {ObtenerEstadoCliente(mascota.IDCliente)}");

        }

        private static string LimpiarNombre(string nombre)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                nombre = nombre.Replace(c, '_');

            return nombre.Replace(' ', '_');
        }

        private async Task MostrarAviso(string mensaje)
        {
            var dialogo = new ContentDialog
            {
                Title = "Exportación PDF",
                Content = mensaje,
                CloseButtonText = "Aceptar",
                XamlRoot = this.XamlRoot
            };

            await dialogo.ShowAsync();
        }

        private void ExportarClientes(EscritorPDF pdf)
        {
            pdf.Titulo("CLIENTES");

            if (clientes.Count == 0)
            {
                pdf.Texto("No hay clientes registrados.");
                return;
            }

            foreach (string[] p in clientes)
            {
                if (p.Length < 4)
                    continue;

                pdf.Subtitulo($"{p[1].Trim()} — ID {p[0].Trim()}");
                pdf.Texto($"Teléfono: {p[2].Trim()}");
                pdf.Texto($"Correo: {p[3].Trim()}");
                pdf.Texto($"Estado: {ObtenerEstadoCliente(p[0].Trim())}");
                pdf.EspacioPequeno();
            }
        }

        private void ExportarMascotas(EscritorPDF pdf)
        {
            pdf.Titulo("MASCOTAS");

            if (mascotas.Count == 0)
            {
                pdf.Texto("No hay mascotas registradas.");
                return;
            }

            foreach (string[] p in mascotas)
            {
                if (p.Length < 8)
                    continue;

                pdf.Subtitulo($"{p[1].Trim()} — ID {p[0].Trim()}");
                pdf.Texto($"Especie: {p[2].Trim()}");
                pdf.Texto($"Raza: {p[3].Trim()}");
                pdf.Texto($"Edad: {p[4].Trim()}");
                pdf.Texto($"Sexo: {p[5].Trim()}");
                pdf.Texto($"Peso: {p[6].Trim()} kg");
                pdf.Texto($"Propietario: {ObtenerNombreCliente(p[7].Trim())}");
                pdf.EspacioPequeno();
            }
        }

        private void ExportarCitas(EscritorPDF pdf)
        {
            pdf.Titulo("CITAS");

            if (citas.Count == 0)
            {
                pdf.Texto("No hay citas registradas.");
                return;
            }

            foreach (string[] p in citas.OrderBy(p => p.Length > 2 ? p[2] : ""))
            {
                if (p.Length < 5)
                    continue;

                string idMascota = p[1].Trim();

                string[]? mascota = mascotas.FirstOrDefault(m =>
                    m.Length >= 8 &&
                    m[0].Trim().Equals(
                        idMascota, StringComparison.OrdinalIgnoreCase));

                string nombreMascota = mascota != null
                    ? mascota[1].Trim()
                    : "No encontrada";

                string propietario = mascota != null
                    ? ObtenerNombreCliente(mascota[7].Trim())
                    : "No encontrado";

                string observaciones = p.Length > 5
                    ? string.Join(",", p.Skip(5)).Trim()
                    : "Sin observaciones";

                pdf.Subtitulo($"Cita {p[0].Trim()}");
                pdf.Texto($"Fecha: {p[2].Trim()}");
                pdf.Texto($"Hora: {p[3].Trim()}");
                pdf.Texto($"Servicio: {p[4].Trim()}");
                pdf.Texto($"Mascota: {nombreMascota}");
                pdf.Texto($"Propietario: {propietario}");
                pdf.Texto($"Observaciones: {observaciones}");
                pdf.EspacioPequeno();
            }
        }

        private void ExportarHistorialGeneral(EscritorPDF pdf)
        {
            pdf.Titulo("HISTORIAL CLÍNICO");

            if (historial.Count == 0)
            {
                pdf.Texto("No hay consultas clínicas registradas.");
                return;
            }

            foreach (string[] p in historial.OrderByDescending(
                p => p.Length > 2 ? p[2] : ""))
            {
                if (p.Length < 11)
                    continue;

                string idMascota = p[1].Trim();

                string[]? mascota = mascotas.FirstOrDefault(m =>
                    m.Length >= 8 &&
                    m[0].Trim().Equals(
                        idMascota, StringComparison.OrdinalIgnoreCase));

                pdf.Subtitulo($"Consulta del {p[2]}");
                pdf.Texto($"Mascota: {(mascota != null ? mascota[1].Trim() : idMascota)}");
                pdf.Texto($"Propietario: {(mascota != null ? ObtenerNombreCliente(mascota[7].Trim()) : "No encontrado")}");

                EscribirDatosClinicos(pdf, p);
                pdf.EspacioPequeno();
            }
        }

        private void EscribirDatosClinicos(EscritorPDF pdf, string[] p)
        {
            pdf.Texto($"Peso: {p[3]} kg");
            pdf.Texto($"Motivo: {p[4]}");
            pdf.Texto($"Síntomas: {p[5]}");
            pdf.Texto($"Diagnóstico: {p[6]}");
            pdf.Texto($"Tratamiento: {p[7]}");
            pdf.Texto($"Alergias: {p[8]}");
            pdf.Texto($"Antecedentes: {p[9]}");
            pdf.Texto($"Observaciones: {p[10]}");
        }

        private void ExportarCartillaGeneral(EscritorPDF pdf)
        {
            pdf.Titulo("CARTILLA MÉDICA");

            if (cartilla.Count == 0)
            {
                pdf.Texto("No hay registros en la cartilla médica.");
                return;
            }

            foreach (string[] p in cartilla.OrderByDescending(
                p => p.Length > 4 ? p[4] : ""))
            {
                if (p.Length < 7)
                    continue;

                string idMascota = p[1].Trim();

                string[]? mascota = mascotas.FirstOrDefault(m =>
                    m.Length >= 8 &&
                    m[0].Trim().Equals(
                        idMascota, StringComparison.OrdinalIgnoreCase));

                pdf.Subtitulo($"{p[2]} — {p[3]}");
                pdf.Texto($"Mascota: {(mascota != null ? mascota[1].Trim() : idMascota)}");
                pdf.Texto($"Fecha de aplicación: {p[4]}");
                pdf.Texto($"Próxima fecha: {p[5]}");
                pdf.Texto($"Observaciones: {p[6]}");
                pdf.EspacioPequeno();
            }
        }

        private void ExportarExpedienteMascota(
            EscritorPDF pdf, MascotaParaPDF mascota)
        {
            pdf.Titulo("HISTORIAL DE CITAS");

            var citasMascota = ObtenerCitasMascota(mascota.ID);

            if (citasMascota.Count == 0)
                pdf.Texto("No hay citas registradas para esta mascota.");

            foreach (string[] p in citasMascota)
            {
                string observaciones = p.Length > 5
                    ? string.Join(",", p.Skip(5))
                    : "Sin observaciones";

                pdf.Subtitulo($"Cita {p[0]}");
                pdf.Texto($"Fecha: {p[2]} — Hora: {p[3]}");
                pdf.Texto($"Servicio: {p[4]}");
                pdf.Texto($"Observaciones: {observaciones}");
                pdf.EspacioPequeno();
            }

            pdf.Titulo("HISTORIAL CLÍNICO");

            var consultas = ObtenerHistorialMascota(mascota.ID);

            if (consultas.Count == 0)
                pdf.Texto("No hay consultas clínicas registradas.");

            foreach (string[] p in consultas)
            {
                pdf.Subtitulo($"Consulta del {p[2]}");
                EscribirDatosClinicos(pdf, p);
                pdf.EspacioPequeno();
            }

            pdf.Titulo("CARTILLA MÉDICA");

            var registros = ObtenerCartillaMascota(mascota.ID);

            if (registros.Count == 0)
                pdf.Texto("No hay registros de vacunas o desparasitación.");

            foreach (string[] p in registros)
            {
                pdf.Subtitulo($"{p[2]} — {p[3]}");
                pdf.Texto($"Fecha de aplicación: {p[4]}");
                pdf.Texto($"Próxima fecha: {p[5]}");
                pdf.Texto($"Observaciones: {p[6]}");
                pdf.EspacioPequeno();
            }
        }
    }

    internal sealed class EscritorPDF
    {
        private readonly PdfDocument documento;
        private PdfPage? pagina;
        private XGraphics? grafico;
        private double y;

        private const double Margen = 45;
        private const double AnchoUtil = 505;

        private readonly XColor Naranja =
            XColor.FromArgb(242, 140, 0);

        private readonly XFont fuente =
            new("Arial", 10, XFontStyleEx.Regular);

        private readonly XFont negrita =
            new("Arial", 12, XFontStyleEx.Bold);

        private readonly XFont tituloGrande =
            new("Arial", 21, XFontStyleEx.Bold);

        private readonly XFont tituloSeccion =
            new("Arial", 15, XFontStyleEx.Bold);

        public EscritorPDF(PdfDocument documento)
        {
            this.documento = documento;
        }

        public void NuevaPagina()
        {
            grafico?.Dispose();

            pagina = documento.AddPage();
            pagina.Size = PdfSharp.PageSize.A4;
            grafico = XGraphics.FromPdfPage(pagina);

            y = Margen;
            DibujarLogo();
        }


        private void DibujarLogo()
        {
            if (grafico == null || pagina == null)
                return;

            string rutaLogo = Path.Combine(
                AppContext.BaseDirectory,
                "logo_servetsur.png");

            if (File.Exists(rutaLogo))
            {
                try
                {
                    using XImage logo = XImage.FromFile(rutaLogo);

                    double anchoMaximo = 125;
                    double altoMaximo = 65;

                    // Conserva la proporción original del logo.
                    double proporcion = Math.Min(
                        anchoMaximo / logo.PointWidth,
                        altoMaximo / logo.PointHeight);

                    double ancho = logo.PointWidth * proporcion;
                    double alto = logo.PointHeight * proporcion;

                    grafico.DrawImage(
                        logo,
                        Margen,
                        20,
                        ancho,
                        alto);
                }
                catch
                {
                    grafico.DrawString(
                        "SERVETSUR",
                        tituloGrande,
                        new XSolidBrush(Naranja),
                        new XPoint(Margen, 55));
                }
            }
            else
            {
                grafico.DrawString(
                    "SERVETSUR",
                    tituloGrande,
                    new XSolidBrush(Naranja),
                    new XPoint(Margen, 55));
            }

            grafico.DrawLine(
                new XPen(Naranja, 2),
                Margen,
                88,
                pagina.Width.Point - Margen,
                88);

            y = 112;
        }


        public void Encabezado(string texto)
        {
            AsegurarPagina();

            if (grafico == null)
                return;

            grafico.DrawString(
                texto,
                tituloGrande,
                new XSolidBrush(Naranja),
                new XPoint(Margen, y));

            y += 32;

            grafico.DrawString(
                $"Fecha: {DateTime.Now:dd/MM/yyyy}",
                fuente,
                XBrushes.Gray,
                new XPoint(Margen, y));

            y += 25;
        }


        public void Titulo(string texto)
        {
            AsegurarPagina();

            if (grafico == null || pagina == null)
                return;

            RevisarSalto(45);

            grafico.DrawString(
                texto,
                tituloSeccion,
                new XSolidBrush(Naranja),
                new XPoint(Margen, y));

            y += 8;

            grafico.DrawLine(
                new XPen(Naranja, 1),
                Margen,
                y + 10,
                pagina.Width.Point - Margen,
                y + 10);

            y += 25;
        }

        public void Subtitulo(string texto)
        {
            AsegurarPagina();

            DibujarTextoEnvuelto(
                texto,
                negrita,
                19,
                XBrushes.Black);
        }

        public void Texto(string texto)
        {
            AsegurarPagina();

            DibujarTextoEnvuelto(
                texto,
                fuente,
                15,
                XBrushes.Black);
        }

        public void DivisorNaranja()
        {
            AsegurarPagina();

            if (grafico == null)
                return;

            RevisarSalto(10);

            grafico.DrawLine(
                new XPen(Naranja, 1),
                Margen,
                y,
                pagina!.Width.Point - Margen,
                y);

            y += 12;
        }

        public void Espacio()
        {
            y += 12;
            RevisarSalto(20);
        }

        public void EspacioPequeno()
        {
            y += 5;
            RevisarSalto(15);
        }

        private void AsegurarPagina()
        {
            if (pagina == null || grafico == null)
                NuevaPagina();
        }

        private void DibujarTextoEnvuelto(
            string texto,
            XFont tipoFuente,
            double alturaLinea,
            XBrush pincel)
        {
            if (grafico == null)
                return;

            texto ??= "";

            string[] palabras = texto.Split(
                new[] { ' ' },
                StringSplitOptions.RemoveEmptyEntries);

            if (palabras.Length == 0)
            {
                y += alturaLinea;
                RevisarSalto(alturaLinea);
                return;
            }

            string linea = "";

            foreach (string palabra in palabras)
            {
                string candidata = string.IsNullOrEmpty(linea)
                    ? palabra
                    : linea + " " + palabra;

                if (grafico.MeasureString(candidata, tipoFuente).Width
                        > AnchoUtil &&
                    !string.IsNullOrEmpty(linea))
                {
                    DibujarLinea(linea, tipoFuente, alturaLinea, pincel);
                    linea = palabra;
                }
                else
                {
                    linea = candidata;
                }
            }

            if (!string.IsNullOrEmpty(linea))
            {
                DibujarLinea(linea, tipoFuente, alturaLinea, pincel);
            }
        }

        private void DibujarLinea(
            string texto,
            XFont tipoFuente,
            double alturaLinea,
            XBrush pincel)
        {
            RevisarSalto(alturaLinea);

            if (grafico == null)
                return;

            grafico.DrawString(
                texto,
                tipoFuente,
                pincel,
                new XPoint(Margen, y));

            y += alturaLinea;
        }

        private void RevisarSalto(double alturaNecesaria)
        {
            if (pagina == null || grafico == null)
            {
                NuevaPagina();
                return;
            }

            if (y + alturaNecesaria >
                pagina.Height.Point - Margen)
            {
                NuevaPagina();
            }
        }

        public void SiguientePagina()
        {
            NuevaPagina();
        }

        public void Finalizar()
        {
            grafico?.Dispose();
            grafico = null;
            pagina = null;
        }
    }
}