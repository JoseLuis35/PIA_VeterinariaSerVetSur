
using Microsoft.Data.Sqlite;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace VeterinariaServerSurPia.Views
{
    public sealed partial class RegistrarCita : Page
    {
        private bool _guardandoCita = false;

        public RegistrarCita()
        {
            InitializeComponent();

            DpFechaCita.MinYear = DateTimeOffset.Now.Date;
            DpFechaCita.MaxYear = DateTimeOffset.Now.AddYears(5);

            ConfigurarFechaYHoraInicial();

            Loaded += RegistrarCita_Loaded;
        }

        // ==========================================
        // CONFIGURAR FECHA Y HORA INICIAL
        // ==========================================

        private void ConfigurarFechaYHoraInicial()
        {
            DateTime horaInicial = DateTime.Now.AddMinutes(30);

            int minutosAdicionales =
                (5 - (horaInicial.Minute % 5)) % 5;

            horaInicial = horaInicial
                .AddMinutes(minutosAdicionales)
                .AddSeconds(-horaInicial.Second)
                .AddMilliseconds(-horaInicial.Millisecond);

            DpFechaCita.Date = new DateTimeOffset(
                horaInicial.Date,
                DateTimeOffset.Now.Offset);

            TpHoraCita.Time = horaInicial.TimeOfDay;
        }

        private async void RegistrarCita_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            Loaded -= RegistrarCita_Loaded;

            try
            {
                await CargarMascotasAsync();
                await ActualizarIdCitaAsync();
            }
            catch (Exception ex)
            {
                MostrarMensaje(
                    "No se pudo cargar la información: " + ex.Message,
                    true);
            }
        }

        // ==========================================
        // GENERAR ID DE CITA
        // ==========================================

        private async Task<string> GenerarIdCitaAsync(
            SqliteConnection? conexionExistente = null,
            SqliteTransaction? transaccion = null)
        {
            // Si recibimos una conexión existente, NO debemos
            // cerrarla ni disponerla desde este método.
            if (conexionExistente != null)
            {
                return await ObtenerSiguienteIdAsync(
                    conexionExistente,
                    transaccion);
            }

            // Solo se abre y se cierra una conexión cuando
            // este método es responsable de crearla.
            using SqliteConnection conexion =
                await App.Database.OpenConnectionAsync();

            return await ObtenerSiguienteIdAsync(
                conexion,
                null);
        }

        private async Task<string> ObtenerSiguienteIdAsync(
            SqliteConnection conexion,
            SqliteTransaction? transaccion)
        {
            using SqliteCommand comando = conexion.CreateCommand();

            comando.Transaction = transaccion;
            comando.CommandText = "SELECT IdCita FROM Citas;";

            int maximo = 0;

            using SqliteDataReader reader =
                await comando.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                string id = reader.IsDBNull(0)
                    ? ""
                    : reader.GetString(0).Trim();

                Match match = Regex.Match(
                    id,
                    @"^CITA(\d+)$",
                    RegexOptions.IgnoreCase);

                if (match.Success &&
                    int.TryParse(
                        match.Groups[1].Value,
                        out int numero))
                {
                    maximo = Math.Max(maximo, numero);
                }
            }

            return "CITA" + (maximo + 1).ToString("D4");
        }

        private async Task ActualizarIdCitaAsync()
        {
            try
            {
                TxtIdCita.Text = await GenerarIdCitaAsync();
            }
            catch (Exception ex)
            {
                MostrarMensaje(
                    "No se pudo generar el ID de la cita: " + ex.Message,
                    true);
            }
        }

        // ==========================================
        // CARGAR MASCOTAS DESDE SQLITE
        // ==========================================

        private async Task CargarMascotasAsync()
        {
            string? idAnterior =
                (CmbMascota.SelectedItem as ComboBoxItem)?
                .Tag?.ToString();

            CmbMascota.Items.Clear();

            using SqliteConnection conexion =
                await App.Database.OpenConnectionAsync();

            using SqliteCommand comando = conexion.CreateCommand();

            comando.CommandText = @"
                SELECT IdMascota, Nombre
                FROM Mascotas
                ORDER BY Nombre;";

            using SqliteDataReader reader =
                await comando.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                string id = reader.IsDBNull(0)
                    ? ""
                    : reader.GetString(0).Trim();

                string nombre = reader.IsDBNull(1)
                    ? ""
                    : reader.GetString(1).Trim();

                if (string.IsNullOrWhiteSpace(id) ||
                    string.IsNullOrWhiteSpace(nombre))
                {
                    continue;
                }

                CmbMascota.Items.Add(new ComboBoxItem
                {
                    Content = $"{nombre} ({id})",
                    Tag = id
                });
            }

            ComboBoxItem? anterior = CmbMascota.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(item =>
                    string.Equals(
                        item.Tag?.ToString(),
                        idAnterior,
                        StringComparison.OrdinalIgnoreCase));

            CmbMascota.SelectedItem = anterior;

            if (CmbMascota.Items.Count > 0)
            {
                MostrarMensaje(
                    "Mascotas cargadas. Selecciona la mascota para la cita.",
                    false);
            }
            else
            {
                MostrarMensaje(
                    "No hay mascotas registradas. Registra una mascota antes de programar una cita.",
                    true);
            }
        }

        private async void BtnActualizarMascotas_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                await CargarMascotasAsync();
            }
            catch (Exception ex)
            {
                MostrarMensaje(
                    "No se pudo actualizar la lista de mascotas: "
                    + ex.Message,
                    true);
            }
        }

        // ==========================================
        // VALIDAR OBSERVACIONES
        // ==========================================

        private void TxtObservaciones_TextChanging(
            TextBox sender,
            TextBoxTextChangingEventArgs args)
        {
            string texto = sender.Text;

            texto = Regex.Replace(
                texto,
                @"[^a-zA-ZáéíóúÁÉÍÓÚüÜñÑ0-9\s.,:;¿?¡!\-]",
                "");

            texto = texto.TrimStart();

            texto = Regex.Replace(
                texto,
                @"[ ]{2,}",
                " ");

            if (texto.Length > 500)
            {
                texto = texto.Substring(0, 500);
            }

            if (sender.Text != texto)
            {
                int posicion = sender.SelectionStart;

                sender.Text = texto;

                sender.SelectionStart =
                    Math.Min(posicion, texto.Length);
            }
        }

        // ==========================================
        // REGISTRAR CITA
        // ==========================================

        private async void BtnGuardarCita_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_guardandoCita)
                return;

            _guardandoCita = true;

            Button? botonGuardar = sender as Button;

            if (botonGuardar != null)
                botonGuardar.IsEnabled = false;

            try
            {
                // Validar mascota seleccionada.
                if (CmbMascota.SelectedItem is not ComboBoxItem mascota)
                {
                    MostrarMensaje(
                        "Selecciona una mascota registrada.",
                        true);
                    return;
                }

                string idMascota =
                    mascota.Tag?.ToString()?.Trim() ?? "";

                if (string.IsNullOrWhiteSpace(idMascota))
                {
                    MostrarMensaje(
                        "No se pudo identificar la mascota seleccionada.",
                        true);
                    return;
                }

                // Validar servicio.
                if (CmbTipoServicio.SelectedItem is not ComboBoxItem servicioItem)
                {
                    MostrarMensaje(
                        "Selecciona el tipo de servicio.",
                        true);
                    return;
                }

                string servicio =
                    servicioItem.Content?.ToString()?.Trim() ?? "";

                if (string.IsNullOrWhiteSpace(servicio))
                {
                    MostrarMensaje(
                        "Selecciona un tipo de servicio válido.",
                        true);
                    return;
                }

                // Validar fecha.
                DateTime fechaSeleccionada =
                    DpFechaCita.Date.Date;

                DateTime fechaActual = DateTime.Now.Date;

                if (fechaSeleccionada.Date < fechaActual)
                {
                    MostrarMensaje(
                        "No puedes registrar una cita en una fecha pasada.",
                        true);
                    return;
                }

                // Validar hora.
                TimeSpan horaSeleccionada = TpHoraCita.Time;

                if (horaSeleccionada < TimeSpan.Zero ||
                    horaSeleccionada >= TimeSpan.FromDays(1))
                {
                    MostrarMensaje(
                        "Selecciona una hora válida para la cita.",
                        true);
                    return;
                }

                DateTime fechaHora = fechaSeleccionada.Date.Add(
                    horaSeleccionada);

                if (fechaHora <= DateTime.Now)
                {
                    MostrarMensaje(
                        "La cita debe programarse para una fecha y hora futuras.",
                        true);
                    return;
                }

                string fecha = fechaSeleccionada.ToString(
                    "dd/MM/yyyy",
                    CultureInfo.InvariantCulture);

                string hora = horaSeleccionada.ToString(
                    @"hh\:mm",
                    CultureInfo.InvariantCulture);

                // Validar observaciones.
                string observaciones =
                    TxtObservaciones.Text.Trim();

                if (observaciones.Length > 500)
                {
                    MostrarMensaje(
                        "Las observaciones no pueden superar los 500 caracteres.",
                        true);
                    return;
                }

                // ==================================
                // GUARDAR EN SQLITE
                // ==================================

                using SqliteConnection conexion =
                    await App.Database.OpenConnectionAsync();

                using SqliteTransaction transaccion =
                    conexion.BeginTransaction();

                // Verificar que la mascota todavía exista.
                using (SqliteCommand verificarMascota =
                    conexion.CreateCommand())
                {
                    verificarMascota.Transaction = transaccion;

                    verificarMascota.CommandText = @"
                        SELECT COUNT(*)
                        FROM Mascotas
                        WHERE IdMascota = $id;";

                    verificarMascota.Parameters.AddWithValue(
                        "$id",
                        idMascota);

                    object? resultado =
                        await verificarMascota.ExecuteScalarAsync();

                    long existe = Convert.ToInt64(
                        resultado ?? 0L,
                        CultureInfo.InvariantCulture);

                    if (existe == 0)
                    {
                        transaccion.Rollback();

                        await CargarMascotasAsync();

                        MostrarMensaje(
                            "La mascota ya no está registrada. Actualiza la lista y selecciona otra.",
                            true);
                        return;
                    }
                }

                // Evitar dos citas en el mismo horario.
                using (SqliteCommand verificarCita =
                    conexion.CreateCommand())
                {
                    verificarCita.Transaction = transaccion;

                    verificarCita.CommandText = @"
                        SELECT COUNT(*)
                        FROM Citas
                        WHERE Fecha = $fecha
                          AND Hora = $hora;";

                    verificarCita.Parameters.AddWithValue(
                        "$fecha",
                        fecha);

                    verificarCita.Parameters.AddWithValue(
                        "$hora",
                        hora);

                    object? resultado =
                        await verificarCita.ExecuteScalarAsync();

                    long citasExistentes = Convert.ToInt64(
                        resultado ?? 0L,
                        CultureInfo.InvariantCulture);

                    if (citasExistentes > 0)
                    {
                        transaccion.Rollback();

                        MostrarMensaje(
                            "Ya existe una cita programada en esa fecha y hora. Selecciona otro horario.",
                            true);
                        return;
                    }
                }

                // Generar ID utilizando la misma conexión.
                // Este método ya NO cierra la conexión recibida.
                string idCita = await GenerarIdCitaAsync(
                    conexion,
                    transaccion);

                // Insertar cita.
                using SqliteCommand guardar =
                    conexion.CreateCommand();

                guardar.Transaction = transaccion;

                guardar.CommandText = @"
                    INSERT INTO Citas
                    (
                        IdCita,
                        IdMascota,
                        Fecha,
                        Hora,
                        Servicio,
                        Observaciones
                    )
                    VALUES
                    (
                        $id,
                        $idMascota,
                        $fecha,
                        $hora,
                        $servicio,
                        $observaciones
                    );";

                guardar.Parameters.AddWithValue(
                    "$id",
                    idCita);

                guardar.Parameters.AddWithValue(
                    "$idMascota",
                    idMascota);

                guardar.Parameters.AddWithValue(
                    "$fecha",
                    fecha);

                guardar.Parameters.AddWithValue(
                    "$hora",
                    hora);

                guardar.Parameters.AddWithValue(
                    "$servicio",
                    servicio);

                guardar.Parameters.AddWithValue(
                    "$observaciones",
                    observaciones);

                await guardar.ExecuteNonQueryAsync();

                // Confirmar cambios.
                transaccion.Commit();

                string mascotaRegistrada =
                    mascota.Content?.ToString() ?? idMascota;

                LimpiarFormulario();

                await ActualizarIdCitaAsync();

                MostrarMensaje(
                    $"¡Cita {idCita} registrada correctamente para {mascotaRegistrada}!",
                    false);
            }
            catch (SqliteException ex)
            {
                MostrarMensaje(
                    "Error de base de datos al guardar la cita: "
                    + ex.Message,
                    true);
            }
            catch (Exception ex)
            {
                MostrarMensaje(
                    "Error al guardar la cita: " + ex.Message,
                    true);
            }
            finally
            {
                _guardandoCita = false;

                if (botonGuardar != null)
                    botonGuardar.IsEnabled = true;
            }
        }

        // ==========================================
        // NAVEGACIÓN
        // ==========================================

        private void BtnIrMascotas_Click(
            object sender,
            RoutedEventArgs e)
        {
            MainWindow.Current?.SeleccionarPantalla("Mascotas");
        }

        // ==========================================
        // MENSAJES
        // ==========================================

        private void MostrarMensaje(
            string texto,
            bool esError)
        {
            TxtMensaje.Text = texto;

            TxtMensaje.Foreground = new SolidColorBrush(
                esError
                    ? Microsoft.UI.Colors.Red
                    : Microsoft.UI.Colors.ForestGreen);

            TxtMensaje.Visibility = Visibility.Visible;
        }

        private void OcultarMensaje()
        {
            TxtMensaje.Text = "";
            TxtMensaje.Visibility = Visibility.Collapsed;
        }

        // ==========================================
        // LIMPIAR FORMULARIO
        // ==========================================

        private void LimpiarFormulario()
        {
            CmbTipoServicio.SelectedIndex = -1;

            TxtObservaciones.Text = "";

            ConfigurarFechaYHoraInicial();

            OcultarMensaje();
        }
    }
}
