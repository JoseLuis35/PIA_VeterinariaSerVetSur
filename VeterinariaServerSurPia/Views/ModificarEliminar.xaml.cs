
using Microsoft.Data.Sqlite;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.UI;

namespace VeterinariaServerSurPia.Views
{
    public sealed class ClienteAdmin
    {
        public string ID { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string Telefono { get; set; } = "";
        public string Correo { get; set; } = "";
        public bool Activo { get; set; } = true;

        public string TextoMostrar => $"{ID} - {Nombre}";
    }

    public sealed class RegistroAdmin
    {
        public string ID { get; set; } = "";
        public string[] Datos { get; set; } = Array.Empty<string>();
        public string Titulo { get; set; } = "";
        public string Detalle { get; set; } = "";
    }

    public sealed partial class ModificarEliminar : Page
    {
        private readonly List<ClienteAdmin> todosClientes = new();

        private string idClienteActual = "";
        private string idMascotaActual = "";
        private string idCitaActual = "";

        private string[] datosCitaActual = Array.Empty<string>();

        private bool cargandoClientes;
        private bool filtrandoTexto;

        private static readonly CultureInfo Cultura =
            CultureInfo.InvariantCulture;

        public ModificarEliminar()
        {
            InitializeComponent();

            LimpiarMascota();
            LimpiarCita();

            Loaded += ModificarEliminar_Loaded;
        }

        // =========================================================
        // REFERENCIAS A BOTONES
        // =========================================================

        private Button? ObtenerBotonDarBajaCliente()
        {
            return FindName("BtnDarBajaCliente") as Button;
        }

        private Button? ObtenerBotonEliminarCliente()
        {
            return FindName("BtnEliminarCliente") as Button;
        }

        // =========================================================
        // CARGA INICIAL
        // =========================================================

        private async void ModificarEliminar_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            Loaded -= ModificarEliminar_Loaded;

            try
            {
                await CargarClientesAsync();
                await CargarCitaPendienteAsync();
            }
            catch (Exception ex)
            {
                MostrarMensaje(
                    $"No se pudieron cargar los datos: {ex.Message}",
                    true);
            }
        }

        // =========================================================
        // UTILIDADES
        // =========================================================

        private static bool MismoId(string a, string b)
        {
            return a.Trim().Equals(
                b.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool LeerFecha(
            string texto,
            out DateTime fecha)
        {
            fecha = DateTime.MinValue;

            if (string.IsNullOrWhiteSpace(texto))
                return false;

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
                Cultura,
                DateTimeStyles.None,
                out fecha))
            {
                fecha = fecha.Date;
                return true;
            }

            if (DateTime.TryParse(
                texto.Trim(),
                CultureInfo.GetCultureInfo("es-MX"),
                DateTimeStyles.None,
                out fecha))
            {
                fecha = fecha.Date;
                return true;
            }

            fecha = DateTime.MinValue;
            return false;
        }

        // Hora actual redondeada a bloques de 5 minutos (igual que el TimePicker).
        private static TimeSpan HoraActualRedondeada()
        {
            TimeSpan ahora = DateTime.Now.TimeOfDay;
            int minutos = (int)Math.Round(ahora.TotalMinutes / 5.0) * 5;

            return TimeSpan.FromMinutes(minutos % (24 * 60));
        }

        private static bool LeerHora(
            string texto,
            out TimeSpan hora)
        {
            hora = TimeSpan.Zero;

            if (string.IsNullOrWhiteSpace(texto))
                return false;

            if (TimeSpan.TryParseExact(
                texto.Trim(),
                new[] { @"hh\:mm", @"h\:mm" },
                Cultura,
                out hora))
            {
                return hora >= TimeSpan.Zero &&
                       hora < TimeSpan.FromDays(1);
            }

            if (DateTime.TryParseExact(
                texto.Trim(),
                new[] { "h:mm tt", "hh:mm tt" },
                CultureInfo.GetCultureInfo("en-US"),
                DateTimeStyles.None,
                out DateTime fechaHora))
            {
                hora = fechaHora.TimeOfDay;
                return true;
            }

            return false;
        }

        private static string LeerTexto(
            SqliteDataReader reader,
            int indice)
        {
            if (reader.IsDBNull(indice))
                return "";

            return Convert.ToString(
                reader.GetValue(indice),
                Cultura) ?? "";
        }

        private static long LeerCantidad(object? resultado)
        {
            return Convert.ToInt64(
                resultado ?? 0L,
                Cultura);
        }

        private void MostrarMensaje(
            string mensaje,
            bool error)
        {
            TxtMensaje.Text = mensaje;

            TxtMensaje.Foreground = new SolidColorBrush(
                error
                    ? Color.FromArgb(255, 190, 55, 45)
                    : Color.FromArgb(255, 45, 125, 75));
        }

        private async Task<bool> Confirmar(
            string titulo,
            string mensaje)
        {
            if (XamlRoot == null)
                return false;

            ContentDialog dialogo = new()
            {
                Title = titulo,
                Content = mensaje,
                PrimaryButtonText = "Confirmar",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            return await dialogo.ShowAsync() ==
                   ContentDialogResult.Primary;
        }

        // =========================================================
        // FILTROS DE TEXTO
        // =========================================================

        private void AplicarFiltro(
            TextBox caja,
            Func<char, bool> caracterPermitido,
            int maximo,
            bool permitirEspacios = false,
            bool permitirGuion = false,
            bool permitirPunto = false,
            bool permitirDosPuntos = false,
            bool permitirParentesis = false,
            bool permitirArroba = false,
            bool permitirGuionBajo = false,
            bool permitirMas = false)
        {
            if (filtrandoTexto)
                return;

            string original = caja.Text;
            int cursor = caja.SelectionStart;
            string resultado = "";

            foreach (char caracter in original)
            {
                if (resultado.Length >= maximo)
                    break;

                bool permitido =
                    caracterPermitido(caracter) ||
                    (permitirEspacios && caracter == ' ') ||
                    (permitirGuion && caracter == '-') ||
                    (permitirPunto && caracter == '.') ||
                    (permitirDosPuntos && caracter == ':') ||
                    (permitirParentesis &&
                        (caracter == '(' || caracter == ')')) ||
                    (permitirArroba && caracter == '@') ||
                    (permitirGuionBajo && caracter == '_') ||
                    (permitirMas && caracter == '+');

                if (!permitido)
                    continue;

                if (caracter == ' ' &&
                    (resultado.Length == 0 ||
                     resultado.EndsWith(" ")))
                {
                    continue;
                }

                resultado += caracter;
            }

            if (resultado == original)
                return;

            filtrandoTexto = true;

            caja.Text = resultado;
            caja.SelectionStart = Math.Min(
                cursor,
                resultado.Length);

            filtrandoTexto = false;
        }

        private static bool EsLetra(char c)
        {
            return char.IsLetter(c);
        }

        private static bool EsLetraONumero(char c)
        {
            return char.IsLetterOrDigit(c);
        }

        private void FiltrarNombre(
            TextBox caja,
            int maximo)
        {
            AplicarFiltro(
                caja,
                EsLetra,
                maximo,
                permitirEspacios: true);
        }

        private void TxtNombreCliente_TextChanging(
            TextBox sender,
            TextBoxTextChangingEventArgs args)
        {
            FiltrarNombre(sender, 80);
        }

        private void TxtTelefonoCliente_TextChanging(
            TextBox sender,
            TextBoxTextChangingEventArgs args)
        {
            AplicarFiltro(
                sender,
                char.IsDigit,
                10);
        }

        private void TxtCorreoCliente_TextChanging(
            TextBox sender,
            TextBoxTextChangingEventArgs args)
        {
            AplicarFiltro(
                sender,
                c =>
                    (c >= 'A' && c <= 'Z') ||
                    (c >= 'a' && c <= 'z') ||
                    char.IsDigit(c),
                100,
                permitirPunto: true,
                permitirArroba: true,
                permitirGuion: true,
                permitirGuionBajo: true,
                permitirMas: true);
        }

        private void TxtNombreMascota_TextChanging(
            TextBox sender,
            TextBoxTextChangingEventArgs args)
        {
            FiltrarNombre(sender, 50);
        }

        private void TxtEspecieMascota_TextChanging(
            TextBox sender,
            TextBoxTextChangingEventArgs args)
        {
            FiltrarNombre(sender, 40);
        }

        private void TxtRazaMascota_TextChanging(
            TextBox sender,
            TextBoxTextChangingEventArgs args)
        {
            AplicarFiltro(
                sender,
                EsLetra,
                60,
                permitirEspacios: true,
                permitirGuion: true);
        }

        private void TxtEdadMascota_TextChanging(
            TextBox sender,
            TextBoxTextChangingEventArgs args)
        {
            AplicarFiltro(
                sender,
                char.IsDigit,
                3);
        }

        private void TxtSexoMascota_TextChanging(
            TextBox sender,
            TextBoxTextChangingEventArgs args)
        {
            FiltrarNombre(sender, 10);
        }

        private void TxtPesoMascota_TextChanging(
            TextBox sender,
            TextBoxTextChangingEventArgs args)
        {
            if (filtrandoTexto)
                return;

            string original = sender.Text;
            string convertido = original.Replace(',', '.');

            string resultado = "";
            bool puntoEncontrado = false;
            int decimales = 0;

            foreach (char c in convertido)
            {
                if (char.IsDigit(c))
                {
                    if (puntoEncontrado)
                    {
                        if (decimales >= 2)
                            continue;

                        decimales++;
                    }

                    if (resultado.Length < 7)
                        resultado += c;
                }
                else if (c == '.' && !puntoEncontrado)
                {
                    puntoEncontrado = true;
                    resultado += c;
                }
            }

            if (resultado == original)
                return;

            int cursor = sender.SelectionStart;

            filtrandoTexto = true;

            sender.Text = resultado;
            sender.SelectionStart = Math.Min(
                cursor,
                resultado.Length);

            filtrandoTexto = false;
        }

        private void TxtServicioCita_TextChanging(
            TextBox sender,
            TextBoxTextChangingEventArgs args)
        {
            AplicarFiltro(
                sender,
                EsLetraONumero,
                80,
                permitirEspacios: true,
                permitirGuion: true,
                permitirPunto: true,
                permitirDosPuntos: true,
                permitirParentesis: true);
        }

        // =========================================================
        // CLIENTES
        // =========================================================

        private async Task CargarClientesAsync(
            string conservarId = "")
        {
            cargandoClientes = true;

            try
            {
                todosClientes.Clear();

                using SqliteConnection conexion =
                    await App.Database.OpenConnectionAsync();

                using SqliteCommand comando =
                    conexion.CreateCommand();

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
                    todosClientes.Add(new ClienteAdmin
                    {
                        ID = LeerTexto(reader, 0),
                        Nombre = LeerTexto(reader, 1),
                        Telefono = LeerTexto(reader, 2),
                        Correo = LeerTexto(reader, 3),
                        Activo = LeerCantidad(reader.GetValue(4)) == 1
                    });
                }

                ActualizarListaClientes();

                if (!string.IsNullOrWhiteSpace(conservarId))
                {
                    ClienteCombo.SelectedItem =
                        todosClientes.FirstOrDefault(c =>
                            MismoId(c.ID, conservarId));
                }
            }
            finally
            {
                cargandoClientes = false;
            }
        }

        private void ActualizarListaClientes()
        {
            string filtro = TxtBuscarCliente.Text.Trim();

            List<ClienteAdmin> lista = todosClientes
                .Where(c =>
                    filtro.Length == 0 ||
                    c.ID.Contains(
                        filtro,
                        StringComparison.OrdinalIgnoreCase) ||
                    c.Nombre.Contains(
                        filtro,
                        StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.Nombre)
                .ToList();

            ClienteCombo.ItemsSource = null;
            ClienteCombo.ItemsSource = lista;

            TxtEstadoCliente.Text =
                $"Clientes encontrados: {lista.Count}";
        }

        // Búsqueda de cliente: solo letras y números
        // (sin espacios ni caracteres especiales).
        // Se valida antes de cambiar el texto, así también bloquea lo pegado.
        private void TxtBuscarCliente_BeforeTextChanging(
            TextBox sender,
            TextBoxBeforeTextChangingEventArgs args)
        {
            foreach (char caracter in args.NewText)
            {
                if (!char.IsLetterOrDigit(caracter))
                {
                    args.Cancel = true;
                    return;
                }
            }
        }

        private void TxtBuscarCliente_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            if (cargandoClientes || ClienteCombo == null)
                return;

            string anterior = idClienteActual;

            cargandoClientes = true;

            try
            {
                ActualizarListaClientes();

                ClienteCombo.SelectedItem =
                    (ClienteCombo.ItemsSource as IEnumerable<ClienteAdmin>)?
                    .FirstOrDefault(c =>
                        MismoId(c.ID, anterior));
            }
            finally
            {
                cargandoClientes = false;
            }

            if (ClienteCombo.SelectedItem == null)
                LimpiarCliente();
        }

        private async void ClienteCombo_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (cargandoClientes)
                return;

            if (ClienteCombo.SelectedItem is ClienteAdmin cliente)
            {
                try
                {
                    await CargarClienteAsync(cliente.ID);
                }
                catch (Exception ex)
                {
                    MostrarMensaje(
                        $"No se pudo cargar el cliente: {ex.Message}",
                        true);
                }
            }
            else
            {
                LimpiarCliente();
            }
        }

        private async Task CargarClienteAsync(string id)
        {
            using SqliteConnection conexion =
                await App.Database.OpenConnectionAsync();

            using SqliteCommand comando =
                conexion.CreateCommand();

            comando.CommandText = @"
                SELECT
                    IdCliente,
                    Nombre,
                    Telefono,
                    Correo,
                    Activo
                FROM Clientes
                WHERE IdCliente = $id;";

            comando.Parameters.AddWithValue("$id", id);

            using SqliteDataReader reader =
                await comando.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                MostrarMensaje("Cliente no encontrado.", true);
                return;
            }

            idClienteActual = LeerTexto(reader, 0);

            TxtIDCliente.Text = idClienteActual;
            TxtNombreCliente.Text = LeerTexto(reader, 1);
            TxtTelefonoCliente.Text = LeerTexto(reader, 2);
            TxtCorreoCliente.Text = LeerTexto(reader, 3);

            bool activo = LeerCantidad(reader.GetValue(4)) == 1;

            TxtEstadoBaja.Text = activo
                ? "Estado: cliente activo."
                : "Estado: cliente dado de baja.";

            Button? botonDarBaja =
                ObtenerBotonDarBajaCliente();

            Button? botonEliminar =
                ObtenerBotonEliminarCliente();

            if (botonDarBaja != null)
            {
                botonDarBaja.Content = activo
                    ? "Dar de baja cliente"
                    : "Cliente dado de baja";

                botonDarBaja.IsEnabled = activo;
            }

            if (botonEliminar != null)
                botonEliminar.IsEnabled = true;

            TxtEstadoCliente.Text =
                $"Cliente seleccionado: {TxtNombreCliente.Text}";

            await CargarMascotasAsync();
            await CargarCitasAsync();
        }

        private void LimpiarCliente()
        {
            idClienteActual = "";

            TxtIDCliente.Text = "";
            TxtNombreCliente.Text = "";
            TxtTelefonoCliente.Text = "";
            TxtCorreoCliente.Text = "";
            TxtEstadoBaja.Text = "";

            Button? botonDarBaja =
                ObtenerBotonDarBajaCliente();

            Button? botonEliminar =
                ObtenerBotonEliminarCliente();

            if (botonDarBaja != null)
                botonDarBaja.IsEnabled = false;

            if (botonEliminar != null)
                botonEliminar.IsEnabled = false;

            MascotasListView.ItemsSource = null;
            CitasListView.ItemsSource = null;

            LimpiarMascota();
            LimpiarCita();
        }

        private async void BtnGuardarCliente_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(idClienteActual))
            {
                MostrarMensaje("Selecciona un cliente.", true);
                return;
            }

            string nombre = TxtNombreCliente.Text.Trim();
            string telefono = TxtTelefonoCliente.Text.Trim();
            string correo = TxtCorreoCliente.Text.Trim();

            if (nombre.Length == 0 ||
                telefono.Length == 0 ||
                correo.Length == 0)
            {
                MostrarMensaje(
                    "Completa el nombre, teléfono y correo.",
                    true);
                return;
            }

            if (nombre.Length > 80 ||
                !Regex.IsMatch(
                    nombre,
                    @"^[\p{L}]+(?: [\p{L}]+)*$"))
            {
                MostrarMensaje(
                    "El nombre solo debe contener letras y espacios sencillos.",
                    true);
                return;
            }

            if (!Regex.IsMatch(telefono, @"^\d{10}$"))
            {
                MostrarMensaje(
                    "El teléfono debe tener exactamente 10 dígitos.",
                    true);
                return;
            }

            if (correo.Length > 100 ||
                !Regex.IsMatch(
                    correo,
                    @"^[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}$"))
            {
                MostrarMensaje(
                    "Escribe un correo electrónico válido.",
                    true);
                return;
            }

            try
            {
                using SqliteConnection conexion =
                    await App.Database.OpenConnectionAsync();

                using SqliteCommand comando =
                    conexion.CreateCommand();

                comando.CommandText = @"
                    UPDATE Clientes
                    SET Nombre = $nombre,
                        Telefono = $telefono,
                        Correo = $correo
                    WHERE IdCliente = $id;";

                comando.Parameters.AddWithValue("$nombre", nombre);
                comando.Parameters.AddWithValue("$telefono", telefono);
                comando.Parameters.AddWithValue("$correo", correo);
                comando.Parameters.AddWithValue("$id", idClienteActual);

                int cambios = await comando.ExecuteNonQueryAsync();

                if (cambios == 0)
                {
                    MostrarMensaje(
                        "No se encontró el cliente para actualizar.",
                        true);
                    return;
                }

                string id = idClienteActual;

                await CargarClientesAsync(id);
                await CargarClienteAsync(id);

                MostrarMensaje(
                    "Cliente actualizado correctamente.",
                    false);
            }
            catch (Exception ex)
            {
                MostrarMensaje(
                    $"No se pudo actualizar el cliente: {ex.Message}",
                    true);
            }
        }

        private async void BtnDarBajaCliente_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(idClienteActual))
            {
                MostrarMensaje("Selecciona un cliente.", true);
                return;
            }

            if (!await Confirmar(
                "Dar de baja cliente",
                "Se conservarán sus datos, mascotas y citas. ¿Deseas continuar?"))
            {
                return;
            }

            try
            {
                using SqliteConnection conexion =
                    await App.Database.OpenConnectionAsync();

                using SqliteCommand comando =
                    conexion.CreateCommand();

                comando.CommandText = @"
                    UPDATE Clientes
                    SET Activo = 0
                    WHERE IdCliente = $id;";

                comando.Parameters.AddWithValue(
                    "$id",
                    idClienteActual);

                int cambios = await comando.ExecuteNonQueryAsync();

                if (cambios == 0)
                {
                    MostrarMensaje("No se encontró el cliente.", true);
                    return;
                }

                string id = idClienteActual;

                await CargarClientesAsync(id);
                await CargarClienteAsync(id);

                MostrarMensaje(
                    "Cliente dado de baja correctamente.",
                    false);
            }
            catch (Exception ex)
            {
                MostrarMensaje(
                    $"No se pudo dar de baja al cliente: {ex.Message}",
                    true);
            }
        }

        private async void BtnEliminarCliente_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(idClienteActual))
            {
                MostrarMensaje("Selecciona un cliente.", true);
                return;
            }

            try
            {
                using SqliteConnection conexion =
                    await App.Database.OpenConnectionAsync();

                using SqliteCommand verificar =
                    conexion.CreateCommand();

                verificar.CommandText = @"
                    SELECT COUNT(*)
                    FROM Mascotas
                    WHERE IdCliente = $id;";

                verificar.Parameters.AddWithValue(
                    "$id",
                    idClienteActual);

                long tieneMascotas = LeerCantidad(
                    await verificar.ExecuteScalarAsync());

                if (tieneMascotas > 0)
                {
                    MostrarMensaje(
                        "No puedes eliminar este cliente porque tiene mascotas registradas. Puedes darlo de baja.",
                        true);
                    return;
                }

                if (!await Confirmar(
                    "Eliminar cliente",
                    "Esta acción eliminará definitivamente al cliente. ¿Deseas continuar?"))
                {
                    return;
                }

                using SqliteCommand eliminar =
                    conexion.CreateCommand();

                eliminar.CommandText = @"
                    DELETE FROM Clientes
                    WHERE IdCliente = $id;";

                eliminar.Parameters.AddWithValue(
                    "$id",
                    idClienteActual);

                int cambios = await eliminar.ExecuteNonQueryAsync();

                if (cambios == 0)
                {
                    MostrarMensaje("No se encontró el cliente.", true);
                    return;
                }

                LimpiarCliente();
                await CargarClientesAsync();

                MostrarMensaje(
                    "Cliente eliminado correctamente.",
                    false);
            }
            catch (SqliteException ex)
            {
                MostrarMensaje(
                    $"No se pudo eliminar el cliente. Puede tener registros relacionados. {ex.Message}",
                    true);
            }
            catch (Exception ex)
            {
                MostrarMensaje(
                    $"Error al eliminar el cliente: {ex.Message}",
                    true);
            }
        }

        // =========================================================
        // MASCOTAS
        // =========================================================

        private async Task CargarMascotasAsync(
            string conservarId = "")
        {
            List<RegistroAdmin> mascotas = new();

            if (string.IsNullOrWhiteSpace(idClienteActual))
            {
                MascotasListView.ItemsSource = null;
                LimpiarMascota();
                return;
            }

            using SqliteConnection conexion =
                await App.Database.OpenConnectionAsync();

            using SqliteCommand comando =
                conexion.CreateCommand();

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
                WHERE IdCliente = $id
                ORDER BY Nombre;";

            comando.Parameters.AddWithValue(
                "$id",
                idClienteActual);

            using SqliteDataReader reader =
                await comando.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                string[] datos =
                {
                    LeerTexto(reader, 0),
                    LeerTexto(reader, 1),
                    LeerTexto(reader, 2),
                    LeerTexto(reader, 3),
                    LeerTexto(reader, 4),
                    LeerTexto(reader, 5),
                    reader.IsDBNull(6)
                        ? ""
                        : Convert.ToDecimal(
                            reader.GetValue(6),
                            Cultura).ToString(Cultura),
                    LeerTexto(reader, 7)
                };

                if (string.IsNullOrWhiteSpace(datos[0]))
                    continue;

                mascotas.Add(new RegistroAdmin
                {
                    ID = datos[0],
                    Datos = datos,
                    Titulo = $"{datos[1]} — ID {datos[0]}",
                    Detalle =
                        $"{datos[2]} · {datos[3]} · Peso: {datos[6]} kg"
                });
            }

            MascotasListView.ItemsSource = null;
            MascotasListView.ItemsSource = mascotas;

            MascotasListView.SelectedItem =
                mascotas.FirstOrDefault(m =>
                    MismoId(m.ID, conservarId));

            if (MascotasListView.SelectedItem == null)
                LimpiarMascota();
        }

        private void MascotasListView_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (MascotasListView.SelectedItem is not RegistroAdmin mascota)
            {
                LimpiarMascota();
                return;
            }

            idMascotaActual = mascota.ID;

            TxtIDMascota.Text = mascota.Datos[0];
            TxtNombreMascota.Text = mascota.Datos[1];
            TxtEspecieMascota.Text = mascota.Datos[2];
            TxtRazaMascota.Text = mascota.Datos[3];
            TxtEdadMascota.Text = mascota.Datos[4];
            TxtSexoMascota.Text = mascota.Datos[5];
            TxtPesoMascota.Text = mascota.Datos[6];
        }

        private void LimpiarMascota()
        {
            idMascotaActual = "";

            TxtIDMascota.Text = "";
            TxtNombreMascota.Text = "";
            TxtEspecieMascota.Text = "";
            TxtRazaMascota.Text = "";
            TxtEdadMascota.Text = "";
            TxtSexoMascota.Text = "";
            TxtPesoMascota.Text = "";
        }

        private async void BtnGuardarMascota_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(idClienteActual))
            {
                MostrarMensaje(
                    "Primero selecciona un cliente.",
                    true);
                return;
            }

            if (string.IsNullOrWhiteSpace(idMascotaActual))
            {
                MostrarMensaje("Selecciona una mascota.", true);
                return;
            }

            string nombre = TxtNombreMascota.Text.Trim();
            string especie = TxtEspecieMascota.Text.Trim();
            string raza = TxtRazaMascota.Text.Trim();
            string edadTexto = TxtEdadMascota.Text.Trim();
            string sexo = TxtSexoMascota.Text.Trim();
            string pesoTexto = TxtPesoMascota.Text.Trim();

            if (nombre.Length == 0 ||
                especie.Length == 0 ||
                edadTexto.Length == 0 ||
                sexo.Length == 0 ||
                pesoTexto.Length == 0)
            {
                MostrarMensaje(
                    "Completa el nombre, especie, edad, sexo y peso de la mascota.",
                    true);
                return;
            }

            if (nombre.Length > 50 ||
                !Regex.IsMatch(
                    nombre,
                    @"^[\p{L}]+(?: [\p{L}]+)*$"))
            {
                MostrarMensaje(
                    "El nombre de la mascota solo debe contener letras y espacios.",
                    true);
                return;
            }

            if (especie.Length > 40 ||
                !Regex.IsMatch(
                    especie,
                    @"^[\p{L}]+(?: [\p{L}]+)*$"))
            {
                MostrarMensaje(
                    "Escribe una especie válida.",
                    true);
                return;
            }

            if (raza.Length > 60 ||
                (raza.Length > 0 &&
                 !Regex.IsMatch(
                    raza,
                    @"^[\p{L}]+(?:[ -][\p{L}]+)*$")))
            {
                MostrarMensaje(
                    "La raza contiene caracteres no permitidos.",
                    true);
                return;
            }

            if (!Regex.IsMatch(edadTexto, @"^\d{1,3}$") ||
                !int.TryParse(
                    edadTexto,
                    NumberStyles.None,
                    Cultura,
                    out int edad) ||
                edad < 0 ||
                edad > 150)
            {
                MostrarMensaje(
                    "La edad debe ser un número entero entre 0 y 150 años.",
                    true);
                return;
            }

            if (!Regex.IsMatch(
                sexo,
                @"^(Macho|Hembra)$",
                RegexOptions.IgnoreCase))
            {
                MostrarMensaje(
                    "El sexo debe ser Macho o Hembra.",
                    true);
                return;
            }

            if (!Regex.IsMatch(
                    pesoTexto,
                    @"^\d{1,5}(\.\d{1,2})?$") ||
                !decimal.TryParse(
                    pesoTexto,
                    NumberStyles.AllowDecimalPoint,
                    Cultura,
                    out decimal peso) ||
                peso <= 0 ||
                peso > 1000)
            {
                MostrarMensaje(
                    "El peso debe ser mayor a 0 y máximo 1000 kg, con hasta dos decimales.",
                    true);
                return;
            }

            try
            {
                using SqliteConnection conexion =
                    await App.Database.OpenConnectionAsync();

                using SqliteCommand comando =
                    conexion.CreateCommand();

                comando.CommandText = @"
                    UPDATE Mascotas
                    SET Nombre = $nombre,
                        Especie = $especie,
                        Raza = $raza,
                        Edad = $edad,
                        Sexo = $sexo,
                        Peso = $peso
                    WHERE IdMascota = $id
                      AND IdCliente = $cliente;";

                comando.Parameters.AddWithValue("$nombre", nombre);
                comando.Parameters.AddWithValue("$especie", especie);
                comando.Parameters.AddWithValue("$raza", raza);
                comando.Parameters.AddWithValue(
                    "$edad",
                    edad.ToString(Cultura));
                comando.Parameters.AddWithValue("$sexo", sexo);
                comando.Parameters.AddWithValue("$peso", (double)peso);
                comando.Parameters.AddWithValue("$id", idMascotaActual);
                comando.Parameters.AddWithValue("$cliente", idClienteActual);

                int cambios = await comando.ExecuteNonQueryAsync();

                if (cambios == 0)
                {
                    MostrarMensaje(
                        "No se encontró la mascota o ya no pertenece a este cliente.",
                        true);
                    return;
                }

                string id = idMascotaActual;

                await CargarMascotasAsync(id);
                await CargarCitasAsync();

                MostrarMensaje(
                    "Mascota actualizada correctamente.",
                    false);
            }
            catch (Exception ex)
            {
                MostrarMensaje(
                    $"No se pudo actualizar la mascota: {ex.Message}",
                    true);
            }
        }

        private async void BtnEliminarMascota_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(idMascotaActual))
            {
                MostrarMensaje("Selecciona una mascota.", true);
                return;
            }

            try
            {
                using SqliteConnection conexion =
                    await App.Database.OpenConnectionAsync();

                // Estas son las tablas relacionadas existentes
                // en la base de datos del proyecto.
                string[] tablasRelacionadas =
                {
                    "Citas",
                    "HistorialMedico",
                    "CartillaMedica"
                };

                foreach (string tabla in tablasRelacionadas)
                {
                    using SqliteCommand verificar =
                        conexion.CreateCommand();

                    verificar.CommandText =
                        $"SELECT COUNT(*) FROM {tabla} WHERE IdMascota = $id;";

                    verificar.Parameters.AddWithValue(
                        "$id",
                        idMascotaActual);

                    long cantidad = LeerCantidad(
                        await verificar.ExecuteScalarAsync());

                    if (cantidad > 0)
                    {
                        MostrarMensaje(
                            $"No puedes eliminar esta mascota porque tiene registros en {tabla}.",
                            true);
                        return;
                    }
                }

                if (!await Confirmar(
                    "Eliminar mascota",
                    "Esta acción eliminará definitivamente la mascota. ¿Deseas continuar?"))
                {
                    return;
                }

                using SqliteCommand eliminar =
                    conexion.CreateCommand();

                eliminar.CommandText = @"
                    DELETE FROM Mascotas
                    WHERE IdMascota = $id
                      AND IdCliente = $cliente;";

                eliminar.Parameters.AddWithValue(
                    "$id",
                    idMascotaActual);

                eliminar.Parameters.AddWithValue(
                    "$cliente",
                    idClienteActual);

                int cambios = await eliminar.ExecuteNonQueryAsync();

                if (cambios == 0)
                {
                    MostrarMensaje("No se encontró la mascota.", true);
                    return;
                }

                await CargarMascotasAsync();
                await CargarCitasAsync();

                MostrarMensaje(
                    "Mascota eliminada correctamente.",
                    false);
            }
            catch (SqliteException ex)
            {
                MostrarMensaje(
                    $"No se pudo eliminar la mascota porque tiene registros relacionados o ocurrió un problema con SQLite: {ex.Message}",
                    true);
            }
            catch (Exception ex)
            {
                MostrarMensaje(
                    $"No se pudo eliminar la mascota: {ex.Message}",
                    true);
            }
        }

        // =========================================================
        // CITAS
        // =========================================================

        private async Task CargarCitasAsync(
            string conservarId = "")
        {
            List<RegistroAdmin> citas = new();

            if (string.IsNullOrWhiteSpace(idClienteActual))
            {
                CitasListView.ItemsSource = null;
                LimpiarCita();
                return;
            }

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
                    m.Nombre
                FROM Citas c
                INNER JOIN Mascotas m
                    ON m.IdMascota = c.IdMascota
                WHERE m.IdCliente = $id
                ORDER BY c.Fecha, c.Hora;";

            comando.Parameters.AddWithValue(
                "$id",
                idClienteActual);

            using SqliteDataReader reader =
                await comando.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                string[] datos =
                {
                    LeerTexto(reader, 0),
                    LeerTexto(reader, 1),
                    LeerTexto(reader, 2),
                    LeerTexto(reader, 3),
                    LeerTexto(reader, 4),
                    LeerTexto(reader, 5)
                };

                string nombreMascota = LeerTexto(reader, 6);

                if (string.IsNullOrWhiteSpace(datos[0]))
                    continue;

                string fechaMostrar = datos[2];

                if (LeerFecha(datos[2], out DateTime fecha))
                {
                    fechaMostrar = fecha.ToString(
                        "dd/MM/yyyy",
                        CultureInfo.GetCultureInfo("es-MX"));
                }

                citas.Add(new RegistroAdmin
                {
                    ID = datos[0],
                    Datos = datos,
                    Titulo =
                        $"Cita {datos[0]} — " +
                        (string.IsNullOrWhiteSpace(nombreMascota)
                            ? "Mascota"
                            : nombreMascota),
                    Detalle =
                        $"Fecha: {fechaMostrar} · Hora: {datos[3]} · Servicio: {datos[4]}"
                });
            }

            CitasListView.ItemsSource = null;
            CitasListView.ItemsSource = citas;

            CitasListView.SelectedItem =
                citas.FirstOrDefault(c =>
                    MismoId(c.ID, conservarId));

            if (CitasListView.SelectedItem == null)
                LimpiarCita();
        }

        private void CitasListView_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (CitasListView.SelectedItem is not RegistroAdmin cita ||
                cita.Datos.Length < 6)
            {
                LimpiarCita();
                return;
            }

            idCitaActual = cita.ID;
            datosCitaActual = cita.Datos;

            TxtIDCita.Text = cita.Datos[0];
            TxtMascotaCita.Text = cita.Datos[1];

            if (LeerFecha(cita.Datos[2], out DateTime fecha))
            {
                FechaCitaPicker.Date = new DateTimeOffset(
                    fecha,
                    DateTimeOffset.Now.Offset);
            }
            else
            {
                FechaCitaPicker.Date = DateTimeOffset.Now;
            }

            if (LeerHora(cita.Datos[3], out TimeSpan horaCita))
            {
                TpHoraCita.Time = horaCita;
            }
            else
            {
                TpHoraCita.Time = HoraActualRedondeada();
            }

            TxtServicioCita.Text = cita.Datos[4];
            TxtObservacionesCita.Text = cita.Datos[5];
        }

        private void LimpiarCita()
        {
            idCitaActual = "";
            datosCitaActual = Array.Empty<string>();

            TxtIDCita.Text = "";
            TxtMascotaCita.Text = "";
            FechaCitaPicker.Date = DateTimeOffset.Now;
            TpHoraCita.Time = HoraActualRedondeada();
            TxtServicioCita.Text = "";
            TxtObservacionesCita.Text = "";
        }

        private async void BtnGuardarCita_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(idCitaActual) ||
                datosCitaActual.Length < 6)
            {
                MostrarMensaje("Selecciona una cita.", true);
                return;
            }

            string servicio = TxtServicioCita.Text.Trim();
            string observaciones =
                TxtObservacionesCita.Text.Trim();

            TimeSpan hora = TpHoraCita.Time;

            if (hora < TimeSpan.Zero ||
                hora >= TimeSpan.FromDays(1))
            {
                MostrarMensaje(
                    "Selecciona una hora válida para la cita.",
                    true);
                return;
            }

            if (servicio.Length == 0)
            {
                MostrarMensaje(
                    "El servicio es obligatorio.",
                    true);
                return;
            }

            if (servicio.Length > 80 ||
                !Regex.IsMatch(
                    servicio,
                    @"^[\p{L}\p{N}]+(?:[ \-.:()/][\p{L}\p{N}]+)*$"))
            {
                MostrarMensaje(
                    "El servicio contiene caracteres no permitidos o supera los 80 caracteres.",
                    true);
                return;
            }

            if (observaciones.Length > 500)
            {
                MostrarMensaje(
                    "Las observaciones no pueden superar los 500 caracteres.",
                    true);
                return;
            }

            DateTime fecha = FechaCitaPicker.Date.Date;
            DateTime fechaHora = fecha.Add(hora);

            if (fechaHora <= DateTime.Now)
            {
                MostrarMensaje(
                    "La nueva fecha y hora deben ser futuras.",
                    true);
                return;
            }

            string fechaDb = fecha.ToString(
                "yyyy-MM-dd",
                Cultura);

            string horaDb = hora.ToString(@"hh\:mm");

            try
            {
                using SqliteConnection conexion =
                    await App.Database.OpenConnectionAsync();

                using SqliteCommand verificar =
                    conexion.CreateCommand();

                verificar.CommandText = @"
                    SELECT COUNT(*)
                    FROM Citas
                    WHERE Fecha = $fecha
                      AND Hora = $hora
                      AND IdCita <> $id;";

                verificar.Parameters.AddWithValue(
                    "$fecha",
                    fechaDb);

                verificar.Parameters.AddWithValue(
                    "$hora",
                    horaDb);

                verificar.Parameters.AddWithValue(
                    "$id",
                    idCitaActual);

                long ocupado = LeerCantidad(
                    await verificar.ExecuteScalarAsync());

                if (ocupado > 0)
                {
                    MostrarMensaje(
                        "Ya existe otra cita registrada en esa fecha y hora.",
                        true);
                    return;
                }

                using SqliteCommand actualizar =
                    conexion.CreateCommand();

                actualizar.CommandText = @"
                    UPDATE Citas
                    SET Fecha = $fecha,
                        Hora = $hora,
                        Servicio = $servicio,
                        Observaciones = $observaciones
                    WHERE IdCita = $id;";

                actualizar.Parameters.AddWithValue(
                    "$fecha",
                    fechaDb);

                actualizar.Parameters.AddWithValue(
                    "$hora",
                    horaDb);

                actualizar.Parameters.AddWithValue(
                    "$servicio",
                    servicio);

                actualizar.Parameters.AddWithValue(
                    "$observaciones",
                    observaciones);

                actualizar.Parameters.AddWithValue(
                    "$id",
                    idCitaActual);

                int cambios = await actualizar.ExecuteNonQueryAsync();

                if (cambios == 0)
                {
                    MostrarMensaje(
                        "No se encontró la cita para actualizar.",
                        true);
                    return;
                }

                string id = idCitaActual;

                await CargarCitasAsync(id);

                MostrarMensaje(
                    "Cita actualizada correctamente.",
                    false);
            }
            catch (Exception ex)
            {
                MostrarMensaje(
                    $"No se pudo actualizar la cita: {ex.Message}",
                    true);
            }
        }

        private async void BtnEliminarCita_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(idCitaActual))
            {
                MostrarMensaje("Selecciona una cita.", true);
                return;
            }

            if (!await Confirmar(
                "Eliminar cita",
                $"¿Deseas eliminar definitivamente la cita {idCitaActual}?"))
            {
                return;
            }

            try
            {
                using SqliteConnection conexion =
                    await App.Database.OpenConnectionAsync();

                using SqliteCommand comando =
                    conexion.CreateCommand();

                comando.CommandText = @"
                    DELETE FROM Citas
                    WHERE IdCita = $id;";

                comando.Parameters.AddWithValue(
                    "$id",
                    idCitaActual);

                int cambios = await comando.ExecuteNonQueryAsync();

                if (cambios == 0)
                {
                    MostrarMensaje("No se encontró la cita.", true);
                    return;
                }

                await CargarCitasAsync();

                MostrarMensaje(
                    "Cita eliminada correctamente.",
                    false);
            }
            catch (Exception ex)
            {
                MostrarMensaje(
                    $"No se pudo eliminar la cita: {ex.Message}",
                    true);
            }
        }

        // =========================================================
        // CITA PROCEDENTE DEL CALENDARIO
        // =========================================================

        private async Task CargarCitaPendienteAsync()
        {
            string idCita = MainWindow.CitaPendienteId;

            if (string.IsNullOrWhiteSpace(idCita))
                return;

            MainWindow.CitaPendienteId = "";

            try
            {
                string idCliente = "";

                using (SqliteConnection conexion =
                    await App.Database.OpenConnectionAsync())
                {
                    using SqliteCommand comando =
                        conexion.CreateCommand();

                    comando.CommandText = @"
                        SELECT m.IdCliente
                        FROM Citas c
                        INNER JOIN Mascotas m
                            ON m.IdMascota = c.IdMascota
                        WHERE c.IdCita = $id;";

                    comando.Parameters.AddWithValue(
                        "$id",
                        idCita);

                    object? resultado =
                        await comando.ExecuteScalarAsync();

                    if (resultado == null ||
                        resultado == DBNull.Value)
                    {
                        MostrarMensaje(
                            "No se encontró la cita seleccionada.",
                            true);
                        return;
                    }

                    idCliente = Convert.ToString(
                        resultado,
                        Cultura) ?? "";
                }

                if (string.IsNullOrWhiteSpace(idCliente))
                {
                    MostrarMensaje(
                        "La cita no tiene un propietario válido.",
                        true);
                    return;
                }

                await CargarClientesAsync(idCliente);

                ClienteAdmin? cliente =
                    todosClientes.FirstOrDefault(c =>
                        MismoId(c.ID, idCliente));

                if (cliente == null)
                {
                    MostrarMensaje(
                        "No se encontró al propietario de la cita.",
                        true);
                    return;
                }

                cargandoClientes = true;

                try
                {
                    TxtBuscarCliente.Text = "";
                    ActualizarListaClientes();
                    ClienteCombo.SelectedItem = cliente;
                }
                finally
                {
                    cargandoClientes = false;
                }

                await CargarClienteAsync(cliente.ID);
                await CargarCitasAsync(idCita);

                MostrarMensaje(
                    $"Cita {idCita} cargada desde el calendario.",
                    false);
            }
            catch (Exception ex)
            {
                MostrarMensaje(
                    $"No se pudo cargar la cita: {ex.Message}",
                    true);
            }
        }

        // =========================================================
        // NAVEGACIÓN
        // =========================================================

        private void BtnVerExpediente_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(idMascotaActual))
            {
                MostrarMensaje(
                    "Selecciona una mascota primero.",
                    true);
                return;
            }

            MainWindow.Current?.SeleccionarPantalla(
                "ConsultarHistorial");
        }

        private void BtnIrBusqueda_Click(
            object sender,
            RoutedEventArgs e)
        {
            MainWindow.Current?.SeleccionarPantalla(
                "Busqueda");
        }
    }
}
