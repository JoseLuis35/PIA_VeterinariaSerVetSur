
using Microsoft.Data.Sqlite;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;

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

        private bool _datosCargados;
        private bool _cargando;
        private bool _exportando;

        static ExportarPDF()
        {
            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
        }

        public ExportarPDF()
        {
            InitializeComponent();
        }

        // =========================================================
        // CARGA INICIAL
        // =========================================================

        private async void ExportarPDF_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            Loaded -= ExportarPDF_Loaded;

            await CargarDatosAsync();
        }

        // =========================================================
        // CARGAR INFORMACIÓN DESDE SQLITE
        // =========================================================

        private async Task CargarDatosAsync()
        {
            if (_cargando || _exportando)
                return;

            _cargando = true;
            _datosCargados = false;

            BtnExportarTodo.IsEnabled = false;
            BtnExportarExpediente.IsEnabled = false;

            TxtEstado.Text =
                "Cargando información desde la base de datos...";

            try
            {
                LimpiarDatos();

                using SqliteConnection conexion =
                    await App.Database.OpenConnectionAsync();

                if (conexion.State != System.Data.ConnectionState.Open)
                {
                    throw new InvalidOperationException(
                        "No se pudo establecer una conexión con la base de datos.");
                }

                // CLIENTES
                using (SqliteCommand comando = conexion.CreateCommand())
                {
                    comando.CommandText = @"
                        SELECT
                            IdCliente,
                            Nombre,
                            Telefono,
                            Correo,
                            Activo
                        FROM Clientes
                        ORDER BY Nombre;";

                    using SqliteDataReader reader =
                        await comando.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        string id = LeerTexto(reader, 0);
                        string nombre = LeerTexto(reader, 1);
                        string telefono = LeerTexto(reader, 2);
                        string correo = LeerTexto(reader, 3);
                        string activo = LeerTexto(reader, 4);

                        if (string.IsNullOrWhiteSpace(id))
                            continue;

                        clientes.Add(new[]
                        {
                            id,
                            nombre,
                            telefono,
                            correo
                        });

                        if (activo == "0")
                            clientesInactivos.Add(id);
                    }
                }

                // MASCOTAS
                using (SqliteCommand comando = conexion.CreateCommand())
                {
                    comando.CommandText = @"
                        SELECT
                            IdMascota,
                            Nombre,
                            Especie,
                            Raza,
                            Edad,
                            Sexo,
                            Peso,
                            IdCliente
                        FROM Mascotas
                        ORDER BY Nombre;";

                    using SqliteDataReader reader =
                        await comando.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        string id = LeerTexto(reader, 0);
                        string nombre = LeerTexto(reader, 1);

                        if (string.IsNullOrWhiteSpace(id) ||
                            string.IsNullOrWhiteSpace(nombre))
                        {
                            continue;
                        }

                        mascotas.Add(new[]
                        {
                            id,
                            nombre,
                            LeerTexto(reader, 2),
                            LeerTexto(reader, 3),
                            LeerTexto(reader, 4),
                            LeerTexto(reader, 5),
                            LeerNumero(reader, 6),
                            LeerTexto(reader, 7)
                        });
                    }
                }

                // CITAS
                using (SqliteCommand comando = conexion.CreateCommand())
                {
                    comando.CommandText = @"
                        SELECT
                            IdCita,
                            IdMascota,
                            Fecha,
                            Hora,
                            Servicio,
                            Observaciones
                        FROM Citas
                        ORDER BY Fecha, Hora;";

                    using SqliteDataReader reader =
                        await comando.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        citas.Add(new[]
                        {
                            LeerTexto(reader, 0),
                            LeerTexto(reader, 1),
                            LeerTexto(reader, 2),
                            LeerTexto(reader, 3),
                            LeerTexto(reader, 4),
                            LeerTexto(reader, 5)
                        });
                    }
                }

                // HISTORIAL MÉDICO
                using (SqliteCommand comando = conexion.CreateCommand())
                {
                    comando.CommandText = @"
                        SELECT
                            IdRegistro,
                            IdMascota,
                            Fecha,
                            Peso,
                            Motivo,
                            Sintomas,
                            Diagnostico,
                            Tratamiento,
                            Alergias,
                            Antecedentes,
                            Observaciones
                        FROM HistorialMedico
                        ORDER BY Fecha DESC;";

                    using SqliteDataReader reader =
                        await comando.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        historial.Add(new[]
                        {
                            LeerTexto(reader, 0),
                            LeerTexto(reader, 1),
                            LeerTexto(reader, 2),
                            LeerNumero(reader, 3),
                            LeerTexto(reader, 4),
                            LeerTexto(reader, 5),
                            LeerTexto(reader, 6),
                            LeerTexto(reader, 7),
                            LeerTexto(reader, 8),
                            LeerTexto(reader, 9),
                            LeerTexto(reader, 10)
                        });
                    }
                }

                // CARTILLA MÉDICA
                using (SqliteCommand comando = conexion.CreateCommand())
                {
                    comando.CommandText = @"
                        SELECT
                            IdRegistro,
                            IdMascota,
                            Tipo,
                            Producto,
                            FechaAplicacion,
                            ProximaFecha,
                            Observaciones
                        FROM CartillaMedica
                        ORDER BY FechaAplicacion DESC;";

                    using SqliteDataReader reader =
                        await comando.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        cartilla.Add(new[]
                        {
                            LeerTexto(reader, 0),
                            LeerTexto(reader, 1),
                            LeerTexto(reader, 2),
                            LeerTexto(reader, 3),
                            LeerTexto(reader, 4),
                            LeerTexto(reader, 5),
                            LeerTexto(reader, 6)
                        });
                    }
                }

                CargarSelectorMascotas();

                _datosCargados = true;

                BtnExportarTodo.IsEnabled = true;
                BtnExportarExpediente.IsEnabled =
                    mascotas.Count > 0;

                TxtEstado.Text =
                    "Información cargada correctamente.\n" +
                    $"Clientes: {clientes.Count} | " +
                    $"Mascotas: {mascotas.Count} | " +
                    $"Citas: {citas.Count} | " +
                    $"Consultas: {historial.Count} | " +
                    $"Cartilla: {cartilla.Count}.";
            }
            catch (Exception ex)
            {
                _datosCargados = false;

                BtnExportarTodo.IsEnabled = false;
                BtnExportarExpediente.IsEnabled = false;

                TxtEstado.Text =
                    "No se pudo cargar la información desde SQLite.";

                await MostrarAviso(
                    "Ocurrió un error al consultar la base de datos:\n\n" +
                    ex.Message);
            }
            finally
            {
                _cargando = false;
            }
        }

        private void LimpiarDatos()
        {
            clientes.Clear();
            mascotas.Clear();
            citas.Clear();
            historial.Clear();
            cartilla.Clear();

            clientesInactivos.Clear();

            MascotaCombo.ItemsSource = null;
            MascotaCombo.SelectedItem = null;
        }

        private static string LeerTexto(
            SqliteDataReader reader,
            int indice)
        {
            if (reader.IsDBNull(indice))
                return "";

            return Convert.ToString(
                reader.GetValue(indice),
                CultureInfo.InvariantCulture)?.Trim() ?? "";
        }

        private static string LeerNumero(
            SqliteDataReader reader,
            int indice)
        {
            if (reader.IsDBNull(indice))
                return "";

            object valor = reader.GetValue(indice);

            if (decimal.TryParse(
                Convert.ToString(valor, CultureInfo.InvariantCulture),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out decimal numero))
            {
                return numero.ToString(
                    "0.##",
                    CultureInfo.InvariantCulture);
            }

            return Convert.ToString(
                valor,
                CultureInfo.InvariantCulture) ?? "";
        }

        // =========================================================
        // SELECTOR DE MASCOTAS
        // =========================================================

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
                .Where(m =>
                    !string.IsNullOrWhiteSpace(m.ID) &&
                    !string.IsNullOrWhiteSpace(m.Nombre))
                .OrderBy(m => m.Nombre)
                .ToList();

            MascotaCombo.ItemsSource = lista;

            if (lista.Count == 0)
            {
                TxtEstado.Text =
                    "No hay mascotas registradas para exportar.";
            }
        }

        private string ObtenerNombreCliente(string idCliente)
        {
            if (string.IsNullOrWhiteSpace(idCliente))
                return "Propietario no registrado";

            string[]? cliente = clientes.FirstOrDefault(p =>
                p.Length >= 2 &&
                p[0].Trim().Equals(
                    idCliente,
                    StringComparison.OrdinalIgnoreCase));

            return cliente == null ||
                   string.IsNullOrWhiteSpace(cliente[1])
                ? "Propietario no encontrado"
                : cliente[1].Trim();
        }

        private string ObtenerEstadoCliente(string idCliente)
        {
            if (string.IsNullOrWhiteSpace(idCliente))
                return "Sin información";

            return clientesInactivos.Contains(idCliente)
                ? "Inactivo (dado de baja)"
                : "Activo";
        }

        // =========================================================
        // FILTROS DE INFORMACIÓN
        // =========================================================

        private List<string[]> ObtenerCitasMascota(string idMascota)
        {
            return citas
                .Where(p =>
                    p.Length >= 5 &&
                    p[1].Trim().Equals(
                        idMascota,
                        StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p[2])
                .ThenBy(p => p[3])
                .ToList();
        }

        private List<string[]> ObtenerHistorialMascota(string idMascota)
        {
            return historial
                .Where(p =>
                    p.Length >= 11 &&
                    p[1].Trim().Equals(
                        idMascota,
                        StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(p => p[2])
                .ToList();
        }

        private List<string[]> ObtenerCartillaMascota(string idMascota)
        {
            return cartilla
                .Where(p =>
                    p.Length >= 7 &&
                    p[1].Trim().Equals(
                        idMascota,
                        StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(p => p[4])
                .ToList();
        }

        // =========================================================
        // VALIDACIONES Y BOTONES
        // =========================================================

        private async void BtnExportarTodo_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!_datosCargados)
            {
                await MostrarAviso(
                    "La información todavía no está disponible. " +
                    "Espera a que termine la carga.");
                return;
            }

            if (_cargando || _exportando)
                return;

            if (!HaySeccionesSeleccionadas())
            {
                await MostrarAviso(
                    "Selecciona al menos una sección para exportar.");
                return;
            }

            await ExportarPDFAsync(null);
        }

        private async void BtnExportarExpediente_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!_datosCargados)
            {
                await MostrarAviso(
                    "La información todavía no está disponible. " +
                    "Espera a que termine la carga.");
                return;
            }

            if (_cargando || _exportando)
                return;

            if (MascotaCombo.SelectedItem is not MascotaParaPDF mascota)
            {
                await MostrarAviso(
                    "Selecciona una mascota antes de exportar su expediente.");
                return;
            }

            if (string.IsNullOrWhiteSpace(mascota.ID))
            {
                await MostrarAviso(
                    "No se pudo identificar la mascota seleccionada.");
                return;
            }

            bool existe = mascotas.Any(p =>
                p.Length >= 2 &&
                p[0].Trim().Equals(
                    mascota.ID,
                    StringComparison.OrdinalIgnoreCase));

            if (!existe)
            {
                await MostrarAviso(
                    "La mascota seleccionada ya no se encuentra en los registros. Actualiza la información.");
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

        // =========================================================
        // EXPORTACIÓN PRINCIPAL
        // =========================================================

        private async Task ExportarPDFAsync(
            MascotaParaPDF? mascotaIndividual)
        {
            if (_exportando)
                return;

            _exportando = true;

            BtnExportarTodo.IsEnabled = false;
            BtnExportarExpediente.IsEnabled = false;

            try
            {
                TxtEstado.Text = "Preparando documento PDF...";

                string nombreArchivo = mascotaIndividual == null
                    ? $"Reporte_Veterinaria_{DateTime.Now:yyyyMMdd_HHmm}"
                    : $"Expediente_{LimpiarNombre(mascotaIndividual.Nombre)}_{DateTime.Now:yyyyMMdd}";

                var picker = new FileSavePicker
                {
                    SuggestedStartLocation =
                        PickerLocationId.DocumentsLibrary,

                    SuggestedFileName = nombreArchivo
                };

                picker.FileTypeChoices.Add(
                    "Documento PDF",
                    new List<string> { ".pdf" });

                var ventana = MainWindow.Current;

                if (ventana == null)
                {
                    throw new InvalidOperationException(
                        "No se encontró la ventana principal de la aplicación.");
                }

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(
                    ventana);

                WinRT.Interop.InitializeWithWindow.Initialize(
                    picker,
                    hwnd);

                StorageFile? archivo =
                    await picker.PickSaveFileAsync();

                if (archivo == null)
                {
                    TxtEstado.Text = "Exportación cancelada.";
                    return;
                }

                using (PdfDocument documento = new PdfDocument())
                {
                    documento.Info.Title = mascotaIndividual == null
                        ? "Reporte general de SERVETSUR"
                        : $"Expediente médico de {mascotaIndividual.Nombre}";

                    documento.Info.Author = "SERVETSUR";
                    documento.Info.Subject =
                        "Reporte de información veterinaria";

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

                        ExportarIdentificacion(
                            escritor,
                            mascotaIndividual);

                        ExportarExpedienteMascota(
                            escritor,
                            mascotaIndividual);
                    }

                    escritor.Finalizar();

                    if (documento.PageCount == 0)
                    {
                        throw new InvalidOperationException(
                            "El documento no contiene páginas para guardar.");
                    }

                    documento.Save(archivo.Path);
                }

                TxtEstado.Text =
                    $"PDF creado correctamente: {archivo.Name}";

                await MostrarAviso(
                    $"El PDF se guardó correctamente en:\n\n{archivo.Path}");
            }
            catch (Exception ex)
            {
                TxtEstado.Text =
                    "No se pudo generar el PDF.";

                await MostrarAviso(
                    "Ocurrió un error al generar el PDF:\n\n" +
                    ex.Message);
            }
            finally
            {
                _exportando = false;

                BtnExportarTodo.IsEnabled = _datosCargados;

                BtnExportarExpediente.IsEnabled =
                    _datosCargados && mascotas.Count > 0;
            }
        }

        // =========================================================
        // EXPEDIENTE INDIVIDUAL
        // =========================================================

        private void ExportarIdentificacion(
            EscritorPDF pdf,
            MascotaParaPDF mascota)
        {
            pdf.Subtitulo("DATOS DE LA MASCOTA");

            pdf.Texto($"Nombre: {Dato(mascota.Nombre)}");
            pdf.Texto($"Número de expediente: {Dato(mascota.ID)}");
            pdf.Texto($"Especie: {Dato(mascota.Especie)}");
            pdf.Texto($"Raza: {Dato(mascota.Raza)}");
            pdf.Texto($"Edad: {Dato(mascota.Edad)}");
            pdf.Texto($"Sexo: {Dato(mascota.Sexo)}");

            pdf.Texto(
                $"Peso actual registrado: " +
                $"{(string.IsNullOrWhiteSpace(mascota.Peso) ? "Sin registro" : mascota.Peso + " kg")}");

            pdf.EspacioPequeno();

            pdf.Subtitulo("DATOS DEL PROPIETARIO");

            pdf.Texto($"Nombre: {Dato(mascota.Propietario)}");
            pdf.Texto($"ID del propietario: {Dato(mascota.IDCliente)}");

            pdf.Texto(
                $"Estado: {ObtenerEstadoCliente(mascota.IDCliente)}");

            pdf.EspacioPequeno();
        }

        private void ExportarExpedienteMascota(
            EscritorPDF pdf,
            MascotaParaPDF mascota)
        {
            // CITAS
            pdf.Titulo("HISTORIAL DE CITAS");

            var citasMascota = ObtenerCitasMascota(mascota.ID);

            if (citasMascota.Count == 0)
            {
                pdf.Texto(
                    "No hay citas registradas para esta mascota.");
            }

            foreach (string[] p in citasMascota)
            {
                pdf.Subtitulo($"Cita {Dato(p[0])}");
                pdf.Texto($"Fecha: {Dato(p[2])} — Hora: {Dato(p[3])}");
                pdf.Texto($"Servicio: {Dato(p[4])}");
                pdf.Texto($"Observaciones: {Dato(p[5])}");

                pdf.EspacioPequeno();
            }

            // HISTORIAL CLÍNICO
            pdf.Titulo("HISTORIAL CLÍNICO");

            var consultas = ObtenerHistorialMascota(mascota.ID);

            if (consultas.Count == 0)
            {
                pdf.Texto(
                    "No hay consultas clínicas registradas.");
            }

            foreach (string[] p in consultas)
            {
                pdf.Subtitulo($"Consulta del {Dato(p[2])}");
                EscribirDatosClinicos(pdf, p);
                pdf.EspacioPequeno();
            }

            // CARTILLA MÉDICA
            pdf.Titulo("CARTILLA MÉDICA");

            var registros = ObtenerCartillaMascota(mascota.ID);

            if (registros.Count == 0)
            {
                pdf.Texto(
                    "No hay registros de vacunas o desparasitación.");
            }

            foreach (string[] p in registros)
            {
                pdf.Subtitulo($"{Dato(p[2])} — {Dato(p[3])}");
                pdf.Texto($"Fecha de aplicación: {Dato(p[4])}");
                pdf.Texto($"Próxima fecha: {Dato(p[5])}");
                pdf.Texto($"Observaciones: {Dato(p[6])}");

                pdf.EspacioPequeno();
            }
        }

        // =========================================================
        // REPORTE DE CLIENTES
        // =========================================================

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

                pdf.Subtitulo(
                    $"{Dato(p[1])} — ID {Dato(p[0])}");

                pdf.Texto($"Teléfono: {Dato(p[2])}");
                pdf.Texto($"Correo: {Dato(p[3])}");

                pdf.Texto(
                    $"Estado: {ObtenerEstadoCliente(p[0].Trim())}");

                pdf.EspacioPequeno();
            }
        }

        // =========================================================
        // REPORTE DE MASCOTAS
        // =========================================================

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

                pdf.Subtitulo(
                    $"{Dato(p[1])} — ID {Dato(p[0])}");

                pdf.Texto($"Especie: {Dato(p[2])}");
                pdf.Texto($"Raza: {Dato(p[3])}");
                pdf.Texto($"Edad: {Dato(p[4])}");
                pdf.Texto($"Sexo: {Dato(p[5])}");

                pdf.Texto(
                    $"Peso: {(string.IsNullOrWhiteSpace(p[6]) ? "Sin registro" : p[6] + " kg")}");

                pdf.Texto(
                    $"Propietario: {ObtenerNombreCliente(p[7].Trim())}");

                pdf.EspacioPequeno();
            }
        }

        // =========================================================
        // REPORTE DE CITAS
        // =========================================================

        private void ExportarCitas(EscritorPDF pdf)
        {
            pdf.Titulo("CITAS");

            if (citas.Count == 0)
            {
                pdf.Texto("No hay citas registradas.");
                return;
            }

            foreach (string[] p in citas
                .Where(p => p.Length >= 5)
                .OrderBy(p => p[2])
                .ThenBy(p => p[3]))
            {
                string idMascota = p[1].Trim();

                string[]? mascota = mascotas.FirstOrDefault(m =>
                    m.Length >= 8 &&
                    m[0].Trim().Equals(
                        idMascota,
                        StringComparison.OrdinalIgnoreCase));

                string nombreMascota = mascota != null
                    ? mascota[1].Trim()
                    : "No encontrada";

                string propietario = mascota != null
                    ? ObtenerNombreCliente(mascota[7].Trim())
                    : "No encontrado";

                string observaciones = p.Length > 5
                    ? Dato(p[5])
                    : "Sin observaciones";

                pdf.Subtitulo($"Cita {Dato(p[0])}");
                pdf.Texto($"Fecha: {Dato(p[2])}");
                pdf.Texto($"Hora: {Dato(p[3])}");
                pdf.Texto($"Servicio: {Dato(p[4])}");
                pdf.Texto($"Mascota: {nombreMascota}");
                pdf.Texto($"Propietario: {propietario}");
                pdf.Texto($"Observaciones: {observaciones}");

                pdf.EspacioPequeno();
            }
        }

        // =========================================================
        // REPORTE DE HISTORIAL CLÍNICO
        // =========================================================

        private void ExportarHistorialGeneral(EscritorPDF pdf)
        {
            pdf.Titulo("HISTORIAL CLÍNICO");

            if (historial.Count == 0)
            {
                pdf.Texto(
                    "No hay consultas clínicas registradas.");
                return;
            }

            foreach (string[] p in historial
                .Where(p => p.Length >= 11)
                .OrderByDescending(p => p[2]))
            {
                string idMascota = p[1].Trim();

                string[]? mascota = mascotas.FirstOrDefault(m =>
                    m.Length >= 8 &&
                    m[0].Trim().Equals(
                        idMascota,
                        StringComparison.OrdinalIgnoreCase));

                pdf.Subtitulo($"Consulta del {Dato(p[2])}");

                pdf.Texto(
                    $"Mascota: {(mascota != null ? Dato(mascota[1]) : idMascota)}");

                pdf.Texto(
                    $"Propietario: {(mascota != null ? ObtenerNombreCliente(mascota[7].Trim()) : "No encontrado")}");

                EscribirDatosClinicos(pdf, p);

                pdf.EspacioPequeno();
            }
        }

        private void EscribirDatosClinicos(
            EscritorPDF pdf,
            string[] p)
        {
            if (p.Length < 11)
                return;

            pdf.Texto(
                $"Peso: {(string.IsNullOrWhiteSpace(p[3]) ? "Sin registro" : p[3] + " kg")}");

            pdf.Texto($"Motivo: {Dato(p[4])}");
            pdf.Texto($"Síntomas: {Dato(p[5])}");
            pdf.Texto($"Diagnóstico: {Dato(p[6])}");
            pdf.Texto($"Tratamiento: {Dato(p[7])}");
            pdf.Texto($"Alergias: {Dato(p[8])}");
            pdf.Texto($"Antecedentes: {Dato(p[9])}");
            pdf.Texto($"Observaciones: {Dato(p[10])}");
        }

        // =========================================================
        // REPORTE DE CARTILLA MÉDICA
        // =========================================================

        private void ExportarCartillaGeneral(EscritorPDF pdf)
        {
            pdf.Titulo("CARTILLA MÉDICA");

            if (cartilla.Count == 0)
            {
                pdf.Texto(
                    "No hay registros en la cartilla médica.");
                return;
            }

            foreach (string[] p in cartilla
                .Where(p => p.Length >= 7)
                .OrderByDescending(p => p[4]))
            {
                string idMascota = p[1].Trim();

                string[]? mascota = mascotas.FirstOrDefault(m =>
                    m.Length >= 8 &&
                    m[0].Trim().Equals(
                        idMascota,
                        StringComparison.OrdinalIgnoreCase));

                pdf.Subtitulo($"{Dato(p[2])} — {Dato(p[3])}");

                pdf.Texto(
                    $"Mascota: {(mascota != null ? Dato(mascota[1]) : idMascota)}");

                pdf.Texto($"Fecha de aplicación: {Dato(p[4])}");
                pdf.Texto($"Próxima fecha: {Dato(p[5])}");
                pdf.Texto($"Observaciones: {Dato(p[6])}");

                pdf.EspacioPequeno();
            }
        }

        // =========================================================
        // UTILIDADES
        // =========================================================

        private static string Dato(string? valor)
        {
            return string.IsNullOrWhiteSpace(valor)
                ? "Sin registro"
                : valor.Trim();
        }

        private static string LimpiarNombre(string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre))
                return "Mascota";

            foreach (char c in Path.GetInvalidFileNameChars())
                nombre = nombre.Replace(c, '_');

            nombre = nombre.Trim().Replace(' ', '_');

            return string.IsNullOrWhiteSpace(nombre)
                ? "Mascota"
                : nombre;
        }

        private async Task MostrarAviso(string mensaje)
        {
            if (XamlRoot == null)
                return;

            var dialogo = new ContentDialog
            {
                Title = "Exportación PDF",
                Content = mensaje,
                CloseButtonText = "Aceptar",
                XamlRoot = XamlRoot
            };

            await dialogo.ShowAsync();
        }
    }

    // =============================================================
    // CLASE PARA ESCRIBIR EL CONTENIDO DEL PDF
    // =============================================================

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
                "Assets",
                "logo_servetsur.png");

            if (!File.Exists(rutaLogo))
            {
                rutaLogo = Path.Combine(
                    AppContext.BaseDirectory,
                    "logo_servetsur.png");
            }

            if (File.Exists(rutaLogo))
            {
                try
                {
                    using XImage logo = XImage.FromFile(rutaLogo);

                    double anchoMaximo = 125;
                    double altoMaximo = 65;

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
                    DibujarNombreVeterinaria();
                }
            }
            else
            {
                DibujarNombreVeterinaria();
            }

            grafico.DrawLine(
                new XPen(Naranja, 2),
                Margen,
                88,
                pagina.Width.Point - Margen,
                88);

            y = 112;
        }

        private void DibujarNombreVeterinaria()
        {
            if (grafico == null)
                return;

            grafico.DrawString(
                "SERVETSUR",
                tituloGrande,
                new XSolidBrush(Naranja),
                new XPoint(Margen, 55));
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

            if (grafico == null || pagina == null)
                return;

            RevisarSalto(10);

            grafico.DrawLine(
                new XPen(Naranja, 1),
                Margen,
                y,
                pagina.Width.Point - Margen,
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

            // Permite conservar saltos de línea en observaciones.
            string[] parrafos = texto.Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n');

            foreach (string parrafo in parrafos)
            {
                string[] palabras = parrafo.Split(
                    new[] { ' ' },
                    StringSplitOptions.RemoveEmptyEntries);

                if (palabras.Length == 0)
                {
                    DibujarLinea(
                        "",
                        tipoFuente,
                        alturaLinea,
                        pincel);

                    continue;
                }

                string linea = "";

                foreach (string palabra in palabras)
                {
                    // Evitar que una palabra demasiado larga
                    // salga de los márgenes del documento.
                    if (grafico.MeasureString(
                        palabra,
                        tipoFuente).Width > AnchoUtil)
                    {
                        if (!string.IsNullOrEmpty(linea))
                        {
                            DibujarLinea(
                                linea,
                                tipoFuente,
                                alturaLinea,
                                pincel);

                            linea = "";
                        }

                        DibujarPalabraLarga(
                            palabra,
                            tipoFuente,
                            alturaLinea,
                            pincel);

                        continue;
                    }

                    string candidata = string.IsNullOrEmpty(linea)
                        ? palabra
                        : linea + " " + palabra;

                    if (grafico.MeasureString(
                            candidata,
                            tipoFuente).Width > AnchoUtil
                        && !string.IsNullOrEmpty(linea))
                    {
                        DibujarLinea(
                            linea,
                            tipoFuente,
                            alturaLinea,
                            pincel);

                        linea = palabra;
                    }
                    else
                    {
                        linea = candidata;
                    }
                }

                if (!string.IsNullOrEmpty(linea))
                {
                    DibujarLinea(
                        linea,
                        tipoFuente,
                        alturaLinea,
                        pincel);
                }
            }
        }

        private void DibujarPalabraLarga(
            string palabra,
            XFont tipoFuente,
            double alturaLinea,
            XBrush pincel)
        {
            if (grafico == null)
                return;

            string fragmento = "";

            foreach (char caracter in palabra)
            {
                string candidato = fragmento + caracter;

                if (grafico.MeasureString(
                        candidato,
                        tipoFuente).Width > AnchoUtil
                    && fragmento.Length > 0)
                {
                    DibujarLinea(
                        fragmento,
                        tipoFuente,
                        alturaLinea,
                        pincel);

                    fragmento = caracter.ToString();
                }
                else
                {
                    fragmento = candidato;
                }
            }

            if (!string.IsNullOrEmpty(fragmento))
            {
                DibujarLinea(
                    fragmento,
                    tipoFuente,
                    alturaLinea,
                    pincel);
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

            if (!string.IsNullOrEmpty(texto))
            {
                grafico.DrawString(
                    texto,
                    tipoFuente,
                    pincel,
                    new XPoint(Margen, y));
            }

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
