using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Text.RegularExpressions;

namespace VeterinariaServerSurPia.Views
{
    public sealed partial class RegistrarCita : Page
    {
        public RegistrarCita()
        {
            InitializeComponent();

            DpFechaCita.MinYear = DateTimeOffset.Now.Date;
            DpFechaCita.MaxYear =
                DateTimeOffset.Now.AddYears(5);

            DpFechaCita.Date = DateTimeOffset.Now;

            CargarMascotas();
            ActualizarIdCita();
        }

        private string ObtenerRuta(string archivo)
        {
            return Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                archivo);
        }

        private string GenerarIdCita()
        {
            string ruta = ObtenerRuta("citas.txt");
            int maximo = 0;

            if (File.Exists(ruta))
            {
                foreach (string linea in File.ReadLines(ruta))
                {
                    string id = linea.Split(',')[0].Trim();

                    Match match = Regex.Match(
                        id,
                        @"^CITA(\d+)$",
                        RegexOptions.IgnoreCase);

                    if (match.Success &&
                        int.TryParse(match.Groups[1].Value,
                            out int numero))
                    {
                        maximo = Math.Max(maximo, numero);
                    }
                }
            }

            return "CITA" + (maximo + 1).ToString("D4");
        }

        private void ActualizarIdCita()
        {
            TxtIdCita.Text = GenerarIdCita();
        }

        private void CargarMascotas()
        {
            string? idAnterior =
                (CmbMascota.SelectedItem as ComboBoxItem)?
                .Tag?.ToString();

            CmbMascota.Items.Clear();

            string rutaMascotas = ObtenerRuta("mascotas.txt");

            if (File.Exists(rutaMascotas))
            {
                foreach (string linea in File.ReadLines(rutaMascotas))
                {
                    string[] datos = linea.Split(',');

                    if (datos.Length >= 8 &&
                        !string.IsNullOrWhiteSpace(datos[0]) &&
                        !string.IsNullOrWhiteSpace(datos[1]))
                    {
                        string id = datos[0].Trim();
                        string nombre = datos[1].Trim();

                        CmbMascota.Items.Add(new ComboBoxItem
                        {
                            Content = $"{nombre} ({id})",
                            Tag = id
                        });
                    }
                }
            }

            var anterior = CmbMascota.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(item =>
                    string.Equals(
                        item.Tag?.ToString(),
                        idAnterior,
                        StringComparison.OrdinalIgnoreCase));

            CmbMascota.SelectedItem = anterior;

            MostrarMensaje(
                CmbMascota.Items.Count > 0
                    ? "Mascotas cargadas. Selecciona la mascota."
                    : "No hay mascotas registradas. Registra una primero.",
                CmbMascota.Items.Count == 0);
        }

        private void BtnActualizarMascotas_Click(
            object sender, RoutedEventArgs e)
        {
            CargarMascotas();
        }

        private void BtnGuardarCita_Click(
            object sender, RoutedEventArgs e)
        {
            if (CmbMascota.SelectedItem is not ComboBoxItem mascota)
            {
                MostrarMensaje(
                    "Selecciona una mascota registrada.", true);
                return;
            }

            string idMascota = mascota.Tag?.ToString() ?? "";

            if (CmbTipoServicio.SelectedItem is not ComboBoxItem servicioItem)
            {
                MostrarMensaje(
                    "Selecciona el tipo de servicio.", true);
                return;
            }

            string servicio =
                servicioItem.Content?.ToString()?.Trim() ?? "";

            DateTime fechaSeleccionada =
                DpFechaCita.Date.Date;

            TimeSpan horaSeleccionada = TpHoraCita.Time;

            DateTime fechaHora = fechaSeleccionada
                .Add(horaSeleccionada);

            if (fechaHora <= DateTime.Now)
            {
                MostrarMensaje(
                    "La cita debe programarse para una fecha y hora futuras.",
                    true);
                return;
            }

            string fecha = fechaSeleccionada.ToString(
                "dd/MM/yyyy", CultureInfo.InvariantCulture);

            string hora = horaSeleccionada.ToString(@"hh\:mm");

            string observaciones = TxtObservaciones.Text.Trim()
                .Replace(",", " ")
                .Replace("\r", " ")
                .Replace("\n", " ");

            if (observaciones.Length > 500)
            {
                MostrarMensaje(
                    "Las observaciones no pueden superar 500 caracteres.",
                    true);
                return;
            }

            try
            {
                string rutaMascotas = ObtenerRuta("mascotas.txt");
                string rutaCitas = ObtenerRuta("citas.txt");

                bool mascotaExiste = File.Exists(rutaMascotas) &&
                    File.ReadLines(rutaMascotas).Any(linea =>
                    {
                        string[] datos = linea.Split(',');

                        return datos.Length >= 8 &&
                            datos[0].Trim().Equals(
                                idMascota,
                                StringComparison.OrdinalIgnoreCase);
                    });

                if (!mascotaExiste)
                {
                    CargarMascotas();
                    MostrarMensaje(
                        "La mascota ya no está registrada. Actualiza la lista.",
                        true);
                    return;
                }

                if (File.Exists(rutaCitas))
                {
                    foreach (string linea in File.ReadLines(rutaCitas))
                    {
                        string[] datos = linea.Split(',');

                        if (datos.Length < 6)
                            continue;

                        bool mismaFecha =
                            datos[2].Trim() == fecha;

                        bool mismaHora =
                            datos[3].Trim() == hora;

                        if (mismaFecha && mismaHora)
                        {
                            MostrarMensaje(
                                "Ya existe una cita programada en esa fecha y hora. Selecciona otro horario.",
                                true);
                            return;
                        }
                    }
                }

                // El ID se calcula justo antes de guardar.
                string idCita = GenerarIdCita();

                string nuevaLinea = string.Join(",", new[]
                {
                    idCita,
                    idMascota,
                    fecha,
                    hora,
                    servicio,
                    observaciones
                });

                if (File.Exists(rutaCitas) &&
                    new FileInfo(rutaCitas).Length > 0)
                {
                    string contenido = File.ReadAllText(rutaCitas);

                    if (!contenido.EndsWith("\n") &&
                        !contenido.EndsWith("\r"))
                    {
                        nuevaLinea =
                            Environment.NewLine + nuevaLinea;
                    }
                }

                File.AppendAllText(
                    rutaCitas,
                    nuevaLinea + Environment.NewLine);

                LimpiarFormulario();
                ActualizarIdCita();

                MostrarMensaje(
                    $"¡Cita {idCita} registrada correctamente para {mascota.Content}!",
                    false);
            }
            catch (Exception ex)
            {
                MostrarMensaje(
                    $"Error al guardar la cita: {ex.Message}",
                    true);
            }
        }

        private void BtnIrMascotas_Click(
            object sender, RoutedEventArgs e)
        {
            MainWindow.Current?.SeleccionarPantalla("Mascotas");
        }

        private void MostrarMensaje(string texto, bool esError)
        {
            TxtMensaje.Text = texto;

            TxtMensaje.Foreground = new SolidColorBrush(
                esError
                    ? Microsoft.UI.Colors.Red
                    : Microsoft.UI.Colors.ForestGreen);

            TxtMensaje.Visibility = Visibility.Visible;
        }

        private void LimpiarFormulario()
        {
            CmbTipoServicio.SelectedIndex = -1;
            TxtObservaciones.Text = "";
            DpFechaCita.Date = DateTimeOffset.Now;
        }
    }
}

