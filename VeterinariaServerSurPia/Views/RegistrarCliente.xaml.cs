
using Microsoft.Data.Sqlite;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.UI;

namespace VeterinariaServerSurPia.Views
{
    public sealed partial class RegistrarCliente : Page
    {
        private bool _validandoTexto = false;
        private bool _actualizandoCorreo = false;
        private bool _guardandoCliente = false;

        public RegistrarCliente()
        {
            InitializeComponent();
            Loaded += RegistrarCliente_Loaded;
        }

        // =====================================================
        // CARGA INICIAL
        // =====================================================

        private async void RegistrarCliente_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            Loaded -= RegistrarCliente_Loaded;
            await MostrarSiguienteIdAsync();
        }

        // =====================================================
        // GENERAR ID AUTOMÁTICO
        // =====================================================

        private async Task<int> ObtenerSiguienteNumeroAsync(
            SqliteConnection conexion,
            SqliteTransaction? transaccion = null)
        {
            using SqliteCommand comando = conexion.CreateCommand();

            comando.Transaction = transaccion;
            comando.CommandText = "SELECT IdCliente FROM Clientes;";

            int ultimoId = 0;

            using SqliteDataReader reader =
                await comando.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                string id = reader.IsDBNull(0)
                    ? ""
                    : reader.GetString(0).Trim();

                Match coincidencia = Regex.Match(
                    id,
                    @"^C(\d+)$",
                    RegexOptions.IgnoreCase
                );

                if (coincidencia.Success &&
                    int.TryParse(
                        coincidencia.Groups[1].Value,
                        out int numero))
                {
                    ultimoId = Math.Max(ultimoId, numero);
                }
            }

            return ultimoId + 1;
        }

        private async Task MostrarSiguienteIdAsync()
        {
            try
            {
                using SqliteConnection conexion =
                    await App.Database.OpenConnectionAsync();

                int siguiente =
                    await ObtenerSiguienteNumeroAsync(conexion);

                TxtIdCliente.Text = $"C{siguiente:D4}";
            }
            catch (Exception)
            {
                TxtIdCliente.Text = "No disponible";

                MostrarMensaje(
                    "No se pudo generar el ID del cliente. Verifica la conexión con la base de datos."
                );
            }
        }

        // =====================================================
        // GUARDAR CLIENTE
        // =====================================================

        private async void BtnGuardarCliente_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_guardandoCliente)
                return;

            string nombre = TxtNombreCliente.Text.Trim();
            string telefono = TxtTelefono.Text.Trim();
            string correo = TxtCorreo.Text.Trim();

            // ---------------------------------------------
            // VALIDAR NOMBRE
            // ---------------------------------------------

            if (string.IsNullOrWhiteSpace(nombre))
            {
                MostrarMensaje("El nombre del cliente es obligatorio.");
                TxtNombreCliente.Focus(FocusState.Programmatic);
                return;
            }

            if (nombre.Length < 3)
            {
                MostrarMensaje(
                    "El nombre debe contener al menos 3 caracteres."
                );

                TxtNombreCliente.Focus(FocusState.Programmatic);
                return;
            }

            if (nombre.Length > 80)
            {
                MostrarMensaje(
                    "El nombre no puede superar los 80 caracteres."
                );

                TxtNombreCliente.Focus(FocusState.Programmatic);
                return;
            }

            if (!NombreValido(nombre))
            {
                MostrarMensaje(
                    "El nombre solo puede contener letras, espacios, puntos, apóstrofes y guiones. No uses números ni símbolos especiales."
                );

                TxtNombreCliente.Focus(FocusState.Programmatic);
                return;
            }

            // ---------------------------------------------
            // VALIDAR QUE EXISTA UN MEDIO DE CONTACTO
            // ---------------------------------------------

            if (string.IsNullOrWhiteSpace(telefono) &&
                string.IsNullOrWhiteSpace(correo))
            {
                MostrarMensaje(
                    "Debes ingresar al menos un teléfono o un correo electrónico."
                );

                TxtTelefono.Focus(FocusState.Programmatic);
                return;
            }

            // ---------------------------------------------
            // VALIDAR TELÉFONO
            // ---------------------------------------------

            if (!string.IsNullOrWhiteSpace(telefono))
            {
                if (!Regex.IsMatch(telefono, @"^[0-9]{10}$"))
                {
                    MostrarMensaje(
                        "El teléfono debe contener exactamente 10 dígitos."
                    );

                    TxtTelefono.Focus(FocusState.Programmatic);
                    return;
                }

                if (telefono.All(c => c == telefono[0]))
                {
                    MostrarMensaje(
                        "El teléfono ingresado no parece ser válido."
                    );

                    TxtTelefono.Focus(FocusState.Programmatic);
                    return;
                }
            }

            // ---------------------------------------------
            // VALIDAR CORREO
            // ---------------------------------------------

            if (!string.IsNullOrWhiteSpace(correo))
            {
                if (correo.Length > 100)
                {
                    MostrarMensaje(
                        "El correo no puede superar los 100 caracteres."
                    );

                    TxtCorreo.Focus(FocusState.Programmatic);
                    return;
                }

                if (!CorreoValido(correo))
                {
                    MostrarMensaje(
                        "El correo electrónico no tiene un formato válido. Revisa el usuario, el símbolo @ y el dominio."
                    );

                    TxtCorreo.Focus(FocusState.Programmatic);
                    return;
                }

                correo = correo.ToLowerInvariant();
            }

            _guardandoCliente = true;

            if (sender is Button botonGuardar)
                botonGuardar.IsEnabled = false;

            try
            {
                using SqliteConnection conexion =
                    await App.Database.OpenConnectionAsync();

                using SqliteTransaction transaccion =
                    conexion.BeginTransaction();

                // ---------------------------------------------
                // COMPROBAR TELÉFONO Y CORREO DUPLICADOS
                // ---------------------------------------------

                using (SqliteCommand verificarContacto =
                    conexion.CreateCommand())
                {
                    verificarContacto.Transaction = transaccion;

                    verificarContacto.CommandText = @"
                        SELECT COUNT(*)
                        FROM Clientes
                        WHERE
                            (
                                $telefono <> ''
                                AND TRIM(COALESCE(Telefono, '')) = $telefono
                            )
                            OR
                            (
                                $correo <> ''
                                AND LOWER(TRIM(COALESCE(Correo, ''))) = $correo
                            );
                    ";

                    verificarContacto.Parameters.AddWithValue(
                        "$telefono",
                        telefono
                    );

                    verificarContacto.Parameters.AddWithValue(
                        "$correo",
                        correo
                    );

                    object? resultado =
                        await verificarContacto.ExecuteScalarAsync();

                    long duplicados = Convert.ToInt64(resultado);

                    if (duplicados > 0)
                    {
                        MostrarMensaje(
                            "Ya existe un cliente registrado con ese teléfono o correo electrónico. Verifica los datos."
                        );

                        if (!string.IsNullOrWhiteSpace(telefono))
                            TxtTelefono.Focus(FocusState.Programmatic);
                        else
                            TxtCorreo.Focus(FocusState.Programmatic);

                        return;
                    }
                }

                // ---------------------------------------------
                // GENERAR ID DENTRO DE LA TRANSACCIÓN
                // ---------------------------------------------

                int siguiente =
                    await ObtenerSiguienteNumeroAsync(
                        conexion,
                        transaccion
                    );

                string id = $"C{siguiente:D4}";

                // ---------------------------------------------
                // INSERTAR CLIENTE
                // ---------------------------------------------

                using SqliteCommand guardar =
                    conexion.CreateCommand();

                guardar.Transaction = transaccion;

                guardar.CommandText = @"
                    INSERT INTO Clientes
                    (
                        IdCliente,
                        Nombre,
                        Telefono,
                        Correo,
                        Activo,
                        FechaRegistro
                    )
                    VALUES
                    (
                        $id,
                        $nombre,
                        $telefono,
                        $correo,
                        1,
                        $fecha
                    );
                ";

                guardar.Parameters.AddWithValue("$id", id);
                guardar.Parameters.AddWithValue("$nombre", nombre);
                guardar.Parameters.AddWithValue("$telefono", telefono);
                guardar.Parameters.AddWithValue("$correo", correo);

                guardar.Parameters.AddWithValue(
                    "$fecha",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                );

                await guardar.ExecuteNonQueryAsync();

                transaccion.Commit();

                // ---------------------------------------------
                // LIMPIAR FORMULARIO Y ACTUALIZAR ID
                // ---------------------------------------------

                LimpiarFormulario();

                await MostrarSiguienteIdAsync();

                MostrarMensaje(
                    $"El cliente {nombre} se registró correctamente con el ID {id}.",
                    false
                );
            }
            catch (SqliteException ex)
            {
                MostrarMensaje(
                    "No se pudo guardar el cliente en la base de datos. Verifica que los datos sean correctos. Detalle: " +
                    ex.Message
                );
            }
            catch (Exception ex)
            {
                MostrarMensaje(
                    "Ocurrió un error al registrar el cliente: " +
                    ex.Message
                );
            }
            finally
            {
                _guardandoCliente = false;

                if (sender is Button boton)
                    boton.IsEnabled = true;
            }
        }

        // =====================================================
        // VALIDACIONES
        // =====================================================

        private bool NombreValido(string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre))
                return false;

            if (nombre != nombre.Trim())
                return false;

            if (nombre.Contains("  "))
                return false;

            // Acepta nombres con acentos, ñ, espacios,
            // puntos, apóstrofes y guiones.
            return Regex.IsMatch(
                nombre,
                @"^\p{L}+(?:[ .'-]\p{L}+)*$"
            );
        }

        private bool CorreoValido(string correo)
        {
            if (string.IsNullOrWhiteSpace(correo))
                return false;

            if (correo != correo.Trim())
                return false;

            if (correo.Length > 100)
                return false;

            string patron =
                @"^[A-Za-z0-9]+(?:[._%+-][A-Za-z0-9]+)*@" +
                @"[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?" +
                @"(?:\.[A-Za-z]{2,})+$";

            return Regex.IsMatch(
                correo,
                patron,
                RegexOptions.IgnoreCase
            );
        }

        // =====================================================
        // MENSAJES VISUALES
        // =====================================================

        private void MostrarMensaje(
            string mensaje,
            bool esError = true)
        {
            TxtMensaje.Text = mensaje;

            if (esError)
            {
                PanelMensaje.Background = new SolidColorBrush(
                    Color.FromArgb(255, 255, 232, 227));

                PanelMensaje.BorderBrush = new SolidColorBrush(
                    Color.FromArgb(255, 245, 198, 189));

                TxtMensaje.Foreground = new SolidColorBrush(
                    Color.FromArgb(255, 192, 57, 43));

                IconoMensaje.Glyph = "\uE7BA";

                IconoMensaje.Foreground = new SolidColorBrush(
                    Color.FromArgb(255, 192, 57, 43));
            }
            else
            {
                PanelMensaje.Background = new SolidColorBrush(
                    Color.FromArgb(255, 233, 245, 230));

                PanelMensaje.BorderBrush = new SolidColorBrush(
                    Color.FromArgb(255, 195, 226, 189));

                TxtMensaje.Foreground = new SolidColorBrush(
                    Color.FromArgb(255, 46, 107, 43));

                IconoMensaje.Glyph = "\uE73E";

                IconoMensaje.Foreground = new SolidColorBrush(
                    Color.FromArgb(255, 46, 107, 43));
            }

            PanelMensaje.Visibility = Visibility.Visible;
        }

        // =====================================================
        // LIMPIAR FORMULARIO
        // =====================================================

        private void LimpiarFormulario()
        {
            TxtNombreCliente.Text = string.Empty;
            TxtTelefono.Text = string.Empty;
            TxtCorreo.Text = string.Empty;

            CmbProveedorCorreo.SelectedIndex = 0;

            TxtMensaje.Text = string.Empty;
            PanelMensaje.Visibility = Visibility.Collapsed;
        }

        // =====================================================
        // NAVEGACIÓN
        // =====================================================

        private void BtnIrMascotas_Click(
            object sender,
            RoutedEventArgs e)
        {
            MainWindow.Current?.SeleccionarPantalla("Mascotas");
        }

        // =====================================================
        // FILTRADO DEL NOMBRE
        // =====================================================

        private void TxtNombreCliente_TextChanging(
            TextBox sender,
            TextBoxTextChangingEventArgs args)
        {
            if (_validandoTexto)
                return;

            string textoOriginal = sender.Text;

            string textoFiltrado = new string(
                textoOriginal
                    .Where(c =>
                        char.IsLetter(c) ||
                        c == ' ' ||
                        c == '.' ||
                        c == '\'' ||
                        c == '-')
                    .Take(80)
                    .ToArray()
            );

            textoFiltrado = Regex.Replace(
                textoFiltrado,
                @"^ +",
                ""
            );

            textoFiltrado = Regex.Replace(
                textoFiltrado,
                @" {2,}",
                " "
            );

            if (textoOriginal == textoFiltrado)
                return;

            int posicionOriginal = sender.SelectionStart;

            int nuevaPosicion = Math.Min(
                posicionOriginal,
                textoFiltrado.Length
            );

            try
            {
                _validandoTexto = true;

                sender.Text = textoFiltrado;
                sender.SelectionStart = nuevaPosicion;
            }
            finally
            {
                _validandoTexto = false;
            }
        }

        // =====================================================
        // FILTRADO DEL TELÉFONO
        // =====================================================

        private void TxtTelefono_TextChanging(
            TextBox sender,
            TextBoxTextChangingEventArgs args)
        {
            if (_validandoTexto)
                return;

            string textoOriginal = sender.Text;

            string textoFiltrado = new string(
                textoOriginal
                    .Where(c => c >= '0' && c <= '9')
                    .Take(10)
                    .ToArray()
            );

            if (textoOriginal == textoFiltrado)
                return;

            int posicionOriginal = sender.SelectionStart;

            int nuevaPosicion = Math.Min(
                posicionOriginal,
                textoFiltrado.Length
            );

            try
            {
                _validandoTexto = true;

                sender.Text = textoFiltrado;
                sender.SelectionStart = nuevaPosicion;
            }
            finally
            {
                _validandoTexto = false;
            }
        }

        // =====================================================
        // FILTRADO DEL CORREO
        // =====================================================

        private void TxtCorreo_TextChanging(
            TextBox sender,
            TextBoxTextChangingEventArgs args)
        {
            if (_validandoTexto || _actualizandoCorreo)
                return;

            string textoOriginal = sender.Text;
            int posicionOriginal = sender.SelectionStart;

            string textoFiltrado = new string(
                textoOriginal
                    .Where(c =>
                        (c >= 'A' && c <= 'Z') ||
                        (c >= 'a' && c <= 'z') ||
                        (c >= '0' && c <= '9') ||
                        c == '.' ||
                        c == '_' ||
                        c == '%' ||
                        c == '+' ||
                        c == '-' ||
                        c == '@')
                    .Take(100)
                    .ToArray()
            );

            if (textoOriginal == textoFiltrado)
                return;

            string prefijoOriginal = textoOriginal.Substring(
                0,
                Math.Min(posicionOriginal, textoOriginal.Length)
            );

            int nuevaPosicion = prefijoOriginal.Count(c =>
                (c >= 'A' && c <= 'Z') ||
                (c >= 'a' && c <= 'z') ||
                (c >= '0' && c <= '9') ||
                c == '.' ||
                c == '_' ||
                c == '%' ||
                c == '+' ||
                c == '-' ||
                c == '@'
            );

            nuevaPosicion = Math.Min(
                nuevaPosicion,
                textoFiltrado.Length
            );

            try
            {
                _validandoTexto = true;

                sender.Text = textoFiltrado;
                sender.SelectionStart = nuevaPosicion;
            }
            finally
            {
                _validandoTexto = false;
            }
        }

        // =====================================================
        // AUTOCOMPLETAR CORREO
        // =====================================================

        private void CmbProveedorCorreo_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (TxtCorreo == null ||
                CmbProveedorCorreo.SelectedItem == null)
            {
                return;
            }

            string? proveedor =
                (CmbProveedorCorreo.SelectedItem as ComboBoxItem)
                ?.Content?.ToString();

            if (string.IsNullOrWhiteSpace(proveedor) ||
                proveedor == "Automático")
            {
                return;
            }

            CompletarDominioCorreo(proveedor);
        }

        private void CompletarDominioCorreo(string proveedor)
        {
            string correo = TxtCorreo.Text.Trim();

            if (string.IsNullOrWhiteSpace(correo))
                return;

            string dominio = proveedor switch
            {
                "Gmail" => "gmail.com",
                "Outlook" => "outlook.com",
                "iCloud" => "icloud.com",
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(dominio))
                return;

            int posicionArroba = correo.IndexOf('@');

            string usuario = posicionArroba >= 0
                ? correo.Substring(0, posicionArroba)
                : correo;

            if (string.IsNullOrWhiteSpace(usuario))
                return;

            string nuevoCorreo = $"{usuario}@{dominio}";

            if (nuevoCorreo.Length > 100)
            {
                MostrarMensaje(
                    "El correo resultante supera el límite de 100 caracteres."
                );

                return;
            }

            try
            {
                _actualizandoCorreo = true;

                TxtCorreo.Text = nuevoCorreo;
                TxtCorreo.SelectionStart = TxtCorreo.Text.Length;
            }
            finally
            {
                _actualizandoCorreo = false;
            }
        }
    }
}
