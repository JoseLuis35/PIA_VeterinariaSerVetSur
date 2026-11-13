
using Microsoft.Data.Sqlite;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;

namespace VeterinariaServerSurPia.Views
{
    public sealed class CitaCalendario
    {
        public string ID { get; set; } = "";
        public string IDMascota { get; set; } = "";
        public DateTime Fecha { get; set; }
        public string Hora { get; set; } = "";
        public string Servicio { get; set; } = "";
        public string Observaciones { get; set; } = "";
        public string NombreMascota { get; set; } = "";
        public string NombrePropietario { get; set; } = "";

        public string Titulo =>
            $"{Hora} · {NombreMascota} — {Servicio}";

        public string Detalle =>
            $"Propietario: {NombrePropietario}\n" +
            $"ID cita: {ID} · ID mascota: {IDMascota}";
    }

    public sealed partial class CalendarioCitas : Page
    {
        private readonly List<CitaCalendario> todasLasCitas = new();

        private readonly CultureInfo cultura =
            new CultureInfo("es-MX");

        private bool calendarioInicializado;
        private bool cargandoCitas;
        private bool actualizandoAgenda;

        private static readonly Color ColorDiaConCita =
            Color.FromArgb(255, 242, 140, 40);

        private static readonly Color ColorTextoDiaConCita =
            Color.FromArgb(255, 255, 255, 255);

        public CalendarioCitas()
        {
            InitializeComponent();

            calendarioInicializado = true;

            CalendarView? calendario = ObtenerCalendario();

            if (calendario != null)
            {
                calendario.SelectedDates.Clear();
                calendario.SelectedDates.Add(
                    new DateTimeOffset(DateTime.Today));
            }

            MostrarAgenda(DateTime.Today);

            Loaded += CalendarioCitas_Loaded;
        }

        private CalendarView? ObtenerCalendario()
        {
            return FindName("Calendario") as CalendarView;
        }

        private async void CalendarioCitas_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            Loaded -= CalendarioCitas_Loaded;

            try
            {
                TxtMensajeAgenda.Text = "Cargando citas...";

                await CargarCitasAsync();

                CalendarView? calendario = ObtenerCalendario();

                DateTime fecha =
                    calendario != null &&
                    calendario.SelectedDates.Count > 0
                        ? calendario.SelectedDates[0].Date
                        : DateTime.Today;

                MostrarAgenda(fecha);

                RefrescarColoresCalendario();
            }
            catch (Exception ex)
            {
                TxtMensajeAgenda.Text =
                    "No se pudieron cargar las citas.";

                await MostrarAviso(
                    $"No se pudieron cargar las citas: {ex.Message}");
            }
        }

        private static string LeerTexto(
            SqliteDataReader reader,
            int indice)
        {
            if (reader.IsDBNull(indice))
                return "";

            return Convert.ToString(
                reader.GetValue(indice),
                CultureInfo.InvariantCulture) ?? "";
        }

        private bool IntentarLeerFecha(
            string texto,
            out DateTime fecha)
        {
            fecha = DateTime.MinValue;

            if (string.IsNullOrWhiteSpace(texto))
                return false;

            string valor = texto.Trim();

            string[] formatos =
            {
                "dd/MM/yyyy",
                "d/M/yyyy",
                "yyyy-MM-dd",
                "yyyy-M-d",
                "dd-MM-yyyy",
                "d-M-yyyy",
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-dd HH:mm",
                "yyyy-MM-ddTHH:mm:ss",
                "yyyy-MM-ddTHH:mm:ss.fff",
                "yyyy-MM-ddTHH:mm",
                "dd/MM/yyyy HH:mm:ss",
                "d/M/yyyy H:mm:ss"
            };

            if (DateTime.TryParseExact(
                valor,
                formatos,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out fecha))
            {
                fecha = fecha.Date;
                return true;
            }

            if (DateTime.TryParse(
                valor,
                cultura,
                DateTimeStyles.None,
                out fecha))
            {
                fecha = fecha.Date;
                return true;
            }

            if (DateTime.TryParse(
                valor,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out fecha))
            {
                fecha = fecha.Date;
                return true;
            }

            fecha = DateTime.MinValue;
            return false;
        }

        private async Task CargarCitasAsync()
        {
            if (cargandoCitas)
                return;

            cargandoCitas = true;

            try
            {
                List<CitaCalendario> citasCargadas = new();

                using SqliteConnection conexion =
                    await App.Database.OpenConnectionAsync();

                using SqliteCommand comando =
                    conexion.CreateCommand();

                comando.CommandText = @"
                    SELECT
                        c.IdCita,
                        c.IdMascota,
                        c.Fecha,
                        c.Hora,
                        c.Servicio,
                        c.Observaciones,
                        m.Nombre AS NombreMascota,
                        cl.Nombre AS NombrePropietario

                    FROM Citas c

                    LEFT JOIN Mascotas m
                        ON m.IdMascota = c.IdMascota

                    LEFT JOIN Clientes cl
                        ON cl.IdCliente = m.IdCliente

                    ORDER BY c.Fecha, c.Hora;";

                using SqliteDataReader reader =
                    await comando.ExecuteReaderAsync();

                HashSet<string> idsCargados =
                    new(StringComparer.OrdinalIgnoreCase);

                while (await reader.ReadAsync())
                {
                    string idCita =
                        LeerTexto(reader, 0).Trim();

                    if (string.IsNullOrWhiteSpace(idCita))
                        continue;

                    if (!idsCargados.Add(idCita))
                        continue;

                    string textoFecha =
                        LeerTexto(reader, 2);

                    if (!IntentarLeerFecha(
                        textoFecha,
                        out DateTime fecha))
                    {
                        continue;
                    }

                    string idMascota =
                        LeerTexto(reader, 1).Trim();

                    string nombreMascota =
                        LeerTexto(reader, 6).Trim();

                    string nombrePropietario =
                        LeerTexto(reader, 7).Trim();

                    citasCargadas.Add(new CitaCalendario
                    {
                        ID = idCita,
                        IDMascota = idMascota,
                        Fecha = fecha,
                        Hora = LeerTexto(reader, 3).Trim(),
                        Servicio = LeerTexto(reader, 4).Trim(),
                        Observaciones = LeerTexto(reader, 5).Trim(),

                        NombreMascota =
                            string.IsNullOrWhiteSpace(nombreMascota)
                                ? "Mascota no encontrada"
                                : nombreMascota,

                        NombrePropietario =
                            string.IsNullOrWhiteSpace(nombrePropietario)
                                ? "Propietario no encontrado"
                                : nombrePropietario
                    });
                }

                todasLasCitas.Clear();
                todasLasCitas.AddRange(citasCargadas);

                ActualizarResumenMes();

                RefrescarColoresCalendario();
            }
            finally
            {
                cargandoCitas = false;
            }
        }

        private void Calendario_SelectedDatesChanged(
            CalendarView sender,
            CalendarViewSelectedDatesChangedEventArgs args)
        {
            if (!calendarioInicializado)
                return;

            if (sender.SelectedDates.Count > 0)
            {
                MostrarAgenda(
                    sender.SelectedDates[0].Date);
            }
            else
            {
                CitasListView.ItemsSource = null;

                TxtTotalDia.Text = "0";

                TxtMensajeAgenda.Text =
                    "Selecciona una fecha para consultar las citas.";

                TxtFechaSeleccionada.Text =
                    "Agenda del día";

                LimpiarDetalle();

                ActualizarResumenMes();
            }
        }

        private void MostrarAgenda(DateTime fecha)
        {
            fecha = fecha.Date;

            TxtFechaSeleccionada.Text =
                $"Agenda del {fecha.ToString(
                    "dddd, dd 'de' MMMM 'de' yyyy",
                    cultura)}";

            List<CitaCalendario> citasDia = todasLasCitas
                .Where(c => c.Fecha.Date == fecha)
                .OrderBy(c => ObtenerHoraOrdenamiento(c.Hora))
                .ThenBy(c => c.ID)
                .ToList();

            CitasListView.SelectedItem = null;
            CitasListView.ItemsSource = citasDia;

            TxtTotalDia.Text =
                citasDia.Count.ToString(cultura);

            TxtMensajeAgenda.Text = citasDia.Count == 0
                ? "No hay citas registradas para esta fecha."
                : $"Hay {citasDia.Count} cita(s) programada(s) para este día.";

            LimpiarDetalle();

            ActualizarResumenMes();
        }

        private static TimeSpan ObtenerHoraOrdenamiento(
            string textoHora)
        {
            if (string.IsNullOrWhiteSpace(textoHora))
                return TimeSpan.MaxValue;

            string[] formatos =
            {
                @"hh\:mm",
                @"h\:mm",
                @"hh\:mm\:ss",
                @"h\:mm\:ss"
            };

            if (TimeSpan.TryParseExact(
                textoHora.Trim(),
                formatos,
                CultureInfo.InvariantCulture,
                out TimeSpan hora))
            {
                return hora;
            }

            if (DateTime.TryParse(
                textoHora,
                CultureInfo.GetCultureInfo("es-MX"),
                DateTimeStyles.None,
                out DateTime fechaHora))
            {
                return fechaHora.TimeOfDay;
            }

            return TimeSpan.MaxValue;
        }

        private void AplicarColorDia(
            CalendarViewDayItem item)
        {
            if (item == null)
                return;

            DateTime fecha = item.Date.Date;

            bool tieneCitas = todasLasCitas.Any(c =>
                c.Fecha.Date == fecha);

            if (tieneCitas)
            {
                item.Background =
                    new SolidColorBrush(ColorDiaConCita);

                item.Foreground =
                    new SolidColorBrush(ColorTextoDiaConCita);

                item.FontWeight =
                    Microsoft.UI.Text.FontWeights.Bold;
            }
            else
            {
                item.ClearValue(
                    Control.BackgroundProperty);

                item.ClearValue(
                    Control.ForegroundProperty);

                item.ClearValue(
                    Control.FontWeightProperty);
            }
        }

        private void Calendario_DayItemChanging(
            CalendarView sender,
            CalendarViewDayItemChangingEventArgs args)
        {
            if (args.InRecycleQueue || args.Item == null)
                return;

            AplicarColorDia(args.Item);
        }

        private void RefrescarColoresCalendario()
        {
            CalendarView? calendario = ObtenerCalendario();

            if (calendario == null)
                return;

            calendario.UpdateLayout();

            ActualizarColoresElementosVisuales(calendario);
        }

        private void ActualizarColoresElementosVisuales(
            DependencyObject elemento)
        {
            if (elemento is CalendarViewDayItem dia)
            {
                AplicarColorDia(dia);
                return;
            }

            int cantidad =
                VisualTreeHelper.GetChildrenCount(elemento);

            for (int i = 0; i < cantidad; i++)
            {
                DependencyObject hijo =
                    VisualTreeHelper.GetChild(elemento, i);

                ActualizarColoresElementosVisuales(hijo);
            }
        }

        private void ActualizarResumenMes()
        {
            if (!calendarioInicializado ||
                TxtTotalMes == null)
            {
                return;
            }

            CalendarView? calendario = ObtenerCalendario();

            DateTime fecha =
                calendario != null &&
                calendario.SelectedDates.Count > 0
                    ? calendario.SelectedDates[0].Date
                    : DateTime.Today;

            int totalMes = todasLasCitas.Count(c =>
                c.Fecha.Year == fecha.Year &&
                c.Fecha.Month == fecha.Month);

            TxtTotalMes.Text =
                totalMes.ToString(cultura);
        }

        private void CitasListView_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (CitasListView.SelectedItem is not CitaCalendario cita)
            {
                LimpiarDetalle();
                return;
            }

            TxtIDCita.Text = cita.ID;

            TxtMascota.Text =
                $"{cita.NombreMascota} ({cita.IDMascota})";

            TxtPropietario.Text =
                cita.NombrePropietario;

            TxtServicio.Text =
                string.IsNullOrWhiteSpace(cita.Servicio)
                    ? "Sin servicio especificado"
                    : cita.Servicio;

            TxtHora.Text =
                string.IsNullOrWhiteSpace(cita.Hora)
                    ? "Sin hora especificada"
                    : cita.Hora;

            TxtObservaciones.Text =
                string.IsNullOrWhiteSpace(cita.Observaciones)
                    ? "Sin observaciones"
                    : cita.Observaciones;
        }

        private void LimpiarDetalle()
        {
            TxtIDCita.Text = "";
            TxtMascota.Text = "";
            TxtPropietario.Text = "";
            TxtServicio.Text = "";
            TxtHora.Text = "";
            TxtObservaciones.Text = "";
        }

        private void BtnIrHoy_Click(
            object sender,
            RoutedEventArgs e)
        {
            CalendarView? calendario = ObtenerCalendario();

            if (calendario == null)
                return;

            DateTimeOffset hoy =
                new DateTimeOffset(DateTime.Today);

            calendario.SelectedDates.Clear();
            calendario.SelectedDates.Add(hoy);

            MostrarAgenda(hoy.Date);

            RefrescarColoresCalendario();
        }

        private async void BtnActualizar_Click(
    object sender,
    RoutedEventArgs e)
        {
            if (actualizandoAgenda)
                return;

            actualizandoAgenda = true;

            Button? botonActualizar = sender as Button;

            if (botonActualizar != null)
                botonActualizar.IsEnabled = false;

            CalendarView? calendario = ObtenerCalendario();

            DateTime fecha =
                calendario != null &&
                calendario.SelectedDates.Count > 0
                    ? calendario.SelectedDates[0].Date
                    : DateTime.Today;

            try
            {
                TxtMensajeAgenda.Text =
                    "Actualizando agenda...";

                await CargarCitasAsync();

                MostrarAgenda(fecha);

                RefrescarColoresCalendario();

                TxtMensajeAgenda.Text =
                    "La agenda se actualizó correctamente. " +
                    (todasLasCitas.Count == 0
                        ? "No hay citas registradas."
                        : $"Se cargaron {todasLasCitas.Count} citas.");
            }
            catch (SqliteException ex)
            {
                TxtMensajeAgenda.Text =
                    "No se pudo actualizar la agenda.";

                await MostrarAviso(
                    $"Ocurrió un problema al consultar la base de datos: {ex.Message}");
            }
            catch (Exception ex)
            {
                TxtMensajeAgenda.Text =
                    "No se pudo actualizar la agenda.";

                await MostrarAviso(
                    $"No se pudo actualizar la agenda: {ex.Message}");
            }
            finally
            {
                actualizandoAgenda = false;

                if (botonActualizar != null)
                    botonActualizar.IsEnabled = true;
            }
        }

        private async void BtnAdministrar_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (CitasListView.SelectedItem is not CitaCalendario cita)
            {
                await MostrarAviso(
                    "Primero selecciona una cita de la agenda.");

                return;
            }

            if (string.IsNullOrWhiteSpace(cita.ID))
            {
                await MostrarAviso(
                    "La cita seleccionada no tiene un identificador válido.");

                return;
            }

            MainWindow.CitaPendienteId = cita.ID;

            MainWindow.Current?.SeleccionarPantalla(
                "ModificarEliminar");
        }

        private async Task MostrarAviso(string mensaje)
        {
            if (XamlRoot == null)
                return;

            ContentDialog dialogo = new()
            {
                Title = "Calendario de citas",
                Content = mensaje,
                CloseButtonText = "Aceptar",
                XamlRoot = XamlRoot
            };

            await dialogo.ShowAsync();
        }
    }
}
