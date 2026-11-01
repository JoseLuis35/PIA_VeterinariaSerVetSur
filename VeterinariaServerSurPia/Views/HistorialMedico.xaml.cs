
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace VeterinariaServerSurPia.Views
{
    public sealed class MascotaExpediente
    {
        public string IDMascota { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string Especie { get; set; } = "";
        public string Raza { get; set; } = "";
        public string Peso { get; set; } = "";
        public string IDCliente { get; set; } = "";
        public string NombrePropietario { get; set; } = "";

        public string TextoMostrar =>
            $"{IDMascota} - {Nombre} ({Especie})";
    }

    public sealed class RegistroExpedienteVista
    {
        public string Titulo { get; set; } = "";
        public string Detalle { get; set; } = "";
    }

    public sealed partial class HistorialMedico : Page
    {
        private readonly List<MascotaExpediente> mascotas = new();

        private readonly List<string> registrosClinicos = new();
        private readonly List<string> registrosCartilla = new();

        private string Ruta(string archivo)
        {
            return Path.Combine(AppContext.BaseDirectory, archivo);
        }

        public HistorialMedico()
        {
            InitializeComponent();
            CargarMascotas();
        }

        private void CargarMascotas()
        {
            mascotas.Clear();

            string rutaMascotas = Ruta("mascotas.txt");
            string rutaClientes = Ruta("clientes.txt");

            var clientes = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

            if (File.Exists(rutaClientes))
            {
                foreach (string linea in File.ReadAllLines(rutaClientes))
                {
                    string[] p = linea.Split(',');

                    if (p.Length >= 2)
                    {
                        string id = p[0].Trim();
                        string nombre = p[1].Trim();

                        if (!string.IsNullOrWhiteSpace(id))
                        {
                            clientes[id] = nombre;
                        }
                    }
                }
            }

            if (File.Exists(rutaMascotas))
            {
                foreach (string linea in File.ReadAllLines(rutaMascotas))
                {
                    string[] p = linea.Split(',');

                    if (p.Length < 8)
                    {
                        continue;
                    }

                    string idCliente = p[7].Trim();

                    mascotas.Add(new MascotaExpediente
                    {
                        IDMascota = p[0].Trim(),
                        Nombre = p[1].Trim(),
                        Especie = p[2].Trim(),
                        Raza = p[3].Trim(),
                        Peso = p[6].Trim(),
                        IDCliente = idCliente,
                        NombrePropietario = clientes.TryGetValue(
                            idCliente, out string? nombreCliente)
                                ? nombreCliente
                                : "Propietario no encontrado"
                    });
                }
            }

            MascotaCombo.ItemsSource = null;
            MascotaCombo.ItemsSource = mascotas;

            if (mascotas.Count == 0)
            {
                MostrarMensaje(
                    "No hay mascotas registradas. Primero registra una mascota.",
                    true);
            }
        }

        private void MascotaCombo_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (MascotaCombo.SelectedItem is not MascotaExpediente mascota)
            {
                LimpiarExpediente();
                return;
            }

            TxtIdMascota.Text = mascota.IDMascota;
            TxtNombreMascota.Text = mascota.Nombre;
            TxtEspecieRaza.Text = $"{mascota.Especie} - {mascota.Raza}";
            TxtPropietario.Text = mascota.NombrePropietario;

            // Utiliza el peso del registro de la mascota como valor inicial.
            TxtPeso.Text = mascota.Peso;

            CargarHistorial(mascota.IDMascota);
            CargarCartilla(mascota.IDMascota);

            TxtMensaje.Text = "";
        }

        private void LimpiarExpediente()
        {
            TxtIdMascota.Text = "—";
            TxtNombreMascota.Text = "—";
            TxtEspecieRaza.Text = "—";
            TxtPropietario.Text = "—";

            HistorialListView.ItemsSource = null;
            CartillaListView.ItemsSource = null;
        }

        private void CargarHistorial(string idMascota)
        {
            registrosClinicos.Clear();

            string ruta = Ruta("historial_medico.txt");

            if (File.Exists(ruta))
            {
                foreach (string linea in File.ReadAllLines(ruta))
                {
                    string[] p = linea.Split('|');

                    if (p.Length >= 11 &&
                        p[1].Equals(
                            idMascota,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        registrosClinicos.Add(linea);
                    }
                }
            }

            var vista = registrosClinicos
                .Select(linea =>
                {
                    string[] p = linea.Split('|');

                    return new RegistroExpedienteVista
                    {
                        Titulo = $"Consulta del {p[2]}",
                        Detalle =
                            $"Peso: {p[3]} kg\n" +
                            $"Motivo: {p[4]}\n" +
                            $"Síntomas: {p[5]}\n" +
                            $"Diagnóstico: {p[6]}\n" +
                            $"Tratamiento: {p[7]}\n" +
                            $"Alergias: {p[8]}\n" +
                            $"Antecedentes: {p[9]}\n" +
                            $"Observaciones: {p[10]}"
                    };
                })
                .Reverse()
                .ToList();

            HistorialListView.ItemsSource = vista;
        }

        private void CargarCartilla(string idMascota)
        {
            registrosCartilla.Clear();

            string ruta = Ruta("cartilla_medica.txt");

            if (File.Exists(ruta))
            {
                foreach (string linea in File.ReadAllLines(ruta))
                {
                    string[] p = linea.Split('|');

                    if (p.Length >= 7 &&
                        p[1].Equals(
                            idMascota,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        registrosCartilla.Add(linea);
                    }
                }
            }

            var vista = registrosCartilla
                .Select(linea =>
                {
                    string[] p = linea.Split('|');

                    return new RegistroExpedienteVista
                    {
                        Titulo = $"{p[2]}: {p[3]}",
                        Detalle =
                            $"Aplicación: {p[4]}\n" +
                            $"Próxima fecha: {p[5]}\n" +
                            $"Observaciones: {p[6]}"
                    };
                })
                .Reverse()
                .ToList();

            CartillaListView.ItemsSource = vista;
        }

        private bool HaySeparadorInvalido(params string[] valores)
        {
            return valores.Any(v => v.Contains('|'));
        }

        private void BtnGuardarConsulta_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (MascotaCombo.SelectedItem is not MascotaExpediente mascota)
            {
                MostrarMensaje(
                    "Primero selecciona una mascota.",
                    true);
                return;
            }

            if (!decimal.TryParse(
                    TxtPeso.Text.Trim().Replace(',', '.'),
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out decimal peso) || peso <= 0)
            {
                MostrarMensaje(
                    "Escribe un peso válido mayor que cero.",
                    true);
                return;
            }

            string[] valores =
            {
                TxtMotivo.Text.Trim(),
                TxtSintomas.Text.Trim(),
                TxtDiagnostico.Text.Trim(),
                TxtTratamiento.Text.Trim(),
                TxtAlergias.Text.Trim(),
                TxtAntecedentes.Text.Trim(),
                TxtObservaciones.Text.Trim()
            };

            if (HaySeparadorInvalido(valores))
            {
                MostrarMensaje(
                    "No utilices el símbolo | en los campos de texto.",
                    true);
                return;
            }

            try
            {
                string idRegistro = Guid.NewGuid().ToString("N");
                string fecha = FechaConsultaPicker.Date
                    .ToString("yyyy-MM-dd");

                string[] datos =
                {
                    idRegistro,
                    mascota.IDMascota,
                    fecha,
                    peso.ToString(
                        "0.##",
                        CultureInfo.InvariantCulture),
                    valores[0],
                    valores[1],
                    valores[2],
                    valores[3],
                    string.IsNullOrWhiteSpace(valores[4])
                        ? "No especificadas"
                        : valores[4],
                    string.IsNullOrWhiteSpace(valores[5])
                        ? "No especificados"
                        : valores[5],
                    valores[6]
                };

                File.AppendAllText(
                    Ruta("historial_medico.txt"),
                    string.Join("|", datos) + Environment.NewLine);

                CargarHistorial(mascota.IDMascota);

                MostrarMensaje(
                    "Consulta médica guardada correctamente.",
                    false);

                TxtMotivo.Text = "";
                TxtSintomas.Text = "";
                TxtDiagnostico.Text = "";
                TxtTratamiento.Text = "";
                TxtObservaciones.Text = "";

                // Se conservan las alergias y antecedentes para la siguiente consulta.
                TxtPeso.Text = peso.ToString(
                    "0.##",
                    CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                MostrarMensaje(
                    $"No se pudo guardar la consulta: {ex.Message}",
                    true);
            }
        }

        private void BtnGuardarCartilla_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (MascotaCombo.SelectedItem is not MascotaExpediente mascota)
            {
                MostrarMensaje(
                    "Primero selecciona una mascota.",
                    true);
                return;
            }

            if (TipoCartillaCombo.SelectedItem is not ComboBoxItem tipoItem)
            {
                MostrarMensaje(
                    "Selecciona el tipo de registro.",
                    true);
                return;
            }

            string tipo = tipoItem.Content?.ToString() ?? "Vacuna";
            string producto = TxtProducto.Text.Trim();
            string observaciones = TxtObservacionesCartilla.Text.Trim();

            if (string.IsNullOrWhiteSpace(producto))
            {
                MostrarMensaje(
                    "Escribe el nombre de la vacuna o del producto.",
                    true);
                return;
            }

            if (HaySeparadorInvalido(producto, observaciones))
            {
                MostrarMensaje(
                    "No utilices el símbolo | en los campos de texto.",
                    true);
                return;
            }

            DateTimeOffset aplicacion = FechaAplicacionPicker.Date;
            DateTimeOffset proxima = FechaProximaPicker.Date;

            if (proxima.Date < aplicacion.Date)
            {
                MostrarMensaje(
                    "La próxima fecha no puede ser anterior a la fecha de aplicación.",
                    true);
                return;
            }

            try
            {
                string[] datos =
                {
                    Guid.NewGuid().ToString("N"),
                    mascota.IDMascota,
                    tipo,
                    producto,
                    aplicacion.ToString("yyyy-MM-dd"),
                    proxima.ToString("yyyy-MM-dd"),
                    observaciones
                };

                File.AppendAllText(
                    Ruta("cartilla_medica.txt"),
                    string.Join("|", datos) + Environment.NewLine);

                CargarCartilla(mascota.IDMascota);

                MostrarMensaje(
                    "Registro de cartilla guardado correctamente.",
                    false);

                TxtProducto.Text = "";
                TxtObservacionesCartilla.Text = "";
            }
            catch (Exception ex)
            {
                MostrarMensaje(
                    $"No se pudo guardar el registro: {ex.Message}",
                    true);
            }
        }

        private void MostrarMensaje(string mensaje, bool esError)
        {
            TxtMensaje.Text = mensaje;
            TxtMensaje.Foreground =
                new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.White);

            if (esError)
            {
                TxtMensaje.Foreground =
                    new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Windows.UI.Color.FromArgb(255, 255, 150, 150));
            }
            else
            {
                TxtMensaje.Foreground =
                    new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Windows.UI.Color.FromArgb(255, 158, 217, 181));
            }
        }


        private void BtnConsultarHistorial_Click(
            object sender,
            RoutedEventArgs e)
        {
            MainWindow.Current?.SeleccionarPantalla("ConsultarHistorial");
        }


    }
}
