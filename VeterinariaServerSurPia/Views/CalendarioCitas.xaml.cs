
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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

        private bool calendarioInicializado = false;

        public CalendarioCitas()
        {
            InitializeComponent();

            calendarioInicializado = true;

            Calendario.SelectedDates.Clear();
            Calendario.SelectedDates.Add(DateTimeOffset.Now);

            CargarCitas();
            MostrarAgenda(DateTime.Today);
        }

        private string Ruta(string archivo)
        {
            return Path.Combine(
                AppContext.BaseDirectory,
                archivo);
        }

        private List<string[]> LeerArchivo(string archivo)
        {
            string ruta = Ruta(archivo);

            if (!File.Exists(ruta))
                return new List<string[]>();

            return File.ReadLines(ruta)
                .Where(linea => !string.IsNullOrWhiteSpace(linea))
                .Select(linea => linea.Split(','))
                .ToList();
        }

        private bool IntentarLeerFecha(
            string texto,
            out DateTime fecha)
        {
            string[] formatos =
            {
                "dd/MM/yyyy",
                "d/M/yyyy",
                "yyyy-MM-dd",
                "yyyy-M-d",
                "dd-MM-yyyy",
                "d-M-yyyy"
            };

            if (DateTime.TryParseExact(
                texto.Trim(),
                formatos,
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

        private void CargarCitas()
        {
            todasLasCitas.Clear();

            Dictionary<string, string> clientes =
                new(StringComparer.OrdinalIgnoreCase);

            foreach (string[] p in LeerArchivo("clientes.txt"))
            {
                if (p.Length < 2 ||
                    string.IsNullOrWhiteSpace(p[0]))
                    continue;

                clientes[p[0].Trim()] = p[1].Trim();
            }

            Dictionary<string, string> mascotas =
                new(StringComparer.OrdinalIgnoreCase);

            Dictionary<string, string> propietarios =
                new(StringComparer.OrdinalIgnoreCase);

            foreach (string[] p in LeerArchivo("mascotas.txt"))
            {
                if (p.Length < 8 ||
                    string.IsNullOrWhiteSpace(p[0]))
                    continue;

                string idMascota = p[0].Trim();
                string idCliente = p[7].Trim();

                mascotas[idMascota] = p[1].Trim();

                propietarios[idMascota] =
                    clientes.TryGetValue(
                        idCliente,
                        out string? nombreCliente)
                    ? nombreCliente
                    : "Propietario no encontrado";
            }

            // Evitar mostrar dos veces el mismo ID de cita.
            HashSet<string> idsCargados =
                new(StringComparer.OrdinalIgnoreCase);

            foreach (string[] p in LeerArchivo("citas.txt"))
            {
                if (p.Length < 5)
                    continue;

                string idCita = p[0].Trim();

                if (string.IsNullOrWhiteSpace(idCita))
                    continue;

                if (!IntentarLeerFecha(
                    p[2],
                    out DateTime fecha))
                    continue;

                if (!idsCargados.Add(idCita))
                    continue;

                string idMascota = p[1].Trim();

                todasLasCitas.Add(new CitaCalendario
                {
                    ID = idCita,
                    IDMascota = idMascota,
                    Fecha = fecha,
                    Hora = p[3].Trim(),
                    Servicio = p[4].Trim(),

                    Observaciones = p.Length > 5
                        ? string.Join(",", p.Skip(5)).Trim()
                        : "",

                    NombreMascota = mascotas.TryGetValue(
                        idMascota,
                        out string? nombreMascota)
                        ? nombreMascota
                        : "Mascota no encontrada",

                    NombrePropietario = propietarios.TryGetValue(
                        idMascota,
                        out string? nombrePropietario)
                        ? nombrePropietario
                        : "Propietario no encontrado"
                });
            }

            ActualizarResumenMes();
        }

        private void Calendario_SelectedDatesChanged(
            CalendarView sender,
            CalendarViewSelectedDatesChangedEventArgs args)
        {
            if (!calendarioInicializado)
                return;

            if (sender.SelectedDates.Count > 0)
            {
                MostrarAgenda(sender.SelectedDates[0].Date);
            }
            else
            {
                CitasListView.ItemsSource = null;
                TxtTotalDia.Text = "0";
                TxtMensajeAgenda.Text =
                    "Selecciona una fecha para consultar las citas.";

                LimpiarDetalle();
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
                .OrderBy(c =>
                {
                    return TimeSpan.TryParseExact(
                        c.Hora,
                        @"hh\:mm",
                        CultureInfo.InvariantCulture,
                        out TimeSpan hora)
                        ? hora
                        : TimeSpan.MaxValue;
                })
                .ThenBy(c => c.ID)
                .ToList();

            CitasListView.SelectedItem = null;
            CitasListView.ItemsSource = null;
            CitasListView.ItemsSource = citasDia;

            TxtTotalDia.Text = citasDia.Count.ToString();

            TxtMensajeAgenda.Text = citasDia.Count == 0
                ? "No hay citas registradas para esta fecha."
                : $"Hay {citasDia.Count} cita(s) programada(s) para este día.";

            LimpiarDetalle();
            ActualizarResumenMes();
        }

        private void ActualizarResumenMes()
        {
            if (!calendarioInicializado)
                return;

            DateTime fecha = Calendario.SelectedDates.Count > 0
                ? Calendario.SelectedDates[0].Date
                : DateTime.Today;

            int totalMes = todasLasCitas.Count(c =>
                c.Fecha.Year == fecha.Year &&
                c.Fecha.Month == fecha.Month);

            TxtTotalMes.Text = totalMes.ToString();
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
            TxtPropietario.Text = cita.NombrePropietario;
            TxtServicio.Text = cita.Servicio;
            TxtHora.Text = cita.Hora;
            TxtObservaciones.Text = cita.Observaciones;
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
            Calendario.SelectedDates.Clear();
            Calendario.SelectedDates.Add(DateTimeOffset.Now);

            MostrarAgenda(DateTime.Today);
        }

        private async void BtnActualizar_Click(
            object sender,
            RoutedEventArgs e)
        {
            DateTime fecha = Calendario.SelectedDates.Count > 0
                ? Calendario.SelectedDates[0].Date
                : DateTime.Today;

            try
            {
                CargarCitas();
                MostrarAgenda(fecha);
            }
            catch (Exception ex)
            {
                await MostrarAviso(
                    $"No se pudo actualizar la agenda: {ex.Message}");
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

            MainWindow.CitaPendienteId = cita.ID;
            MainWindow.Current?.SeleccionarPantalla(
                "ModificarEliminar");
        }

        private async Task MostrarAviso(string mensaje)
        {
            ContentDialog dialogo = new()
            {
                Title = "Calendario de citas",
                Content = mensaje,
                CloseButtonText = "Aceptar",
                XamlRoot = this.XamlRoot
            };

            await dialogo.ShowAsync();
        }
    }
}
