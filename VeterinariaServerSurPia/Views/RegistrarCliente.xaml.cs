using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace VeterinariaServerSurPia.Views
{
    public sealed partial class RegistrarCliente : Page
    {
        private bool _validandoTexto = false;
        private bool _actualizandoCorreo = false;

        private readonly string rutaClientes = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "clientes.txt"
        );

        private readonly string rutaConsecutivo = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "ultimo_id_cliente.txt"
        );

        public RegistrarCliente()
        {
            this.InitializeComponent();

            MostrarSiguienteId();
        }

        private int ObtenerSiguienteNumero()
        {
            int ultimoId = 0;

            if (File.Exists(rutaConsecutivo))
            {
                string contenido = File.ReadAllText(rutaConsecutivo).Trim();
                int.TryParse(contenido, out ultimoId);
            }

            if (File.Exists(rutaClientes))
            {
                foreach (string linea in File.ReadAllLines(rutaClientes))
                {
                    string[] datos = linea.Split(',');

                    if (datos.Length > 0)
                    {
                        Match coincidencia = Regex.Match(
                            datos[0].Trim(),
                            @"^C(\d+)$"
                        );

                        if (coincidencia.Success &&
                            int.TryParse(coincidencia.Groups[1].Value, out int numero))
                        {
                            ultimoId = Math.Max(ultimoId, numero);
                        }
                    }
                }
            }

            return ultimoId + 1;
        }

        private void MostrarSiguienteId()
        {
            try
            {
                int siguiente = ObtenerSiguienteNumero();
                TxtIdCliente.Text = $"C{siguiente:D4}";
            }
            catch
            {
                TxtIdCliente.Text = "C0001";
            }
        }

        private void BtnGuardarCliente_Click(object sender, RoutedEventArgs e)
        {
            string id = TxtIdCliente.Text.Trim();
            string nombre = TxtNombreCliente.Text.Trim();
            string telefono = TxtTelefono.Text.Trim();
            string correo = TxtCorreo.Text.Trim();

            if (string.IsNullOrWhiteSpace(nombre))
            {
                MostrarMensaje("Ingresa el nombre del cliente.");
                TxtNombreCliente.Focus(FocusState.Programmatic);
                return;
            }

            if (!NombreValido(nombre))
            {
                MostrarMensaje("El nombre solo debe contener letras y espacios sencillos.");
                TxtNombreCliente.Focus(FocusState.Programmatic);
                return;
            }

            if (nombre.Length > 80)
            {
                MostrarMensaje("El nombre no puede superar los 80 caracteres.");
                TxtNombreCliente.Focus(FocusState.Programmatic);
                return;
            }

            if (string.IsNullOrWhiteSpace(telefono) &&
                string.IsNullOrWhiteSpace(correo))
            {
                MostrarMensaje("Debes ingresar un teléfono o un correo electrónico.");
                TxtTelefono.Focus(FocusState.Programmatic);
                return;
            }

            if (!string.IsNullOrWhiteSpace(telefono) &&
                !Regex.IsMatch(telefono, @"^\d{10}$"))
            {
                MostrarMensaje("El teléfono debe contener exactamente 10 números.");
                TxtTelefono.Focus(FocusState.Programmatic);
                return;
            }

            if (!string.IsNullOrWhiteSpace(correo) &&
                !CorreoValido(correo))
            {
                MostrarMensaje("El correo no es válido. Utiliza únicamente letras, números, puntos y un solo @.");
                TxtCorreo.Focus(FocusState.Programmatic);
                return;
            }

            try
            {
                if (File.Exists(rutaClientes))
                {
                    bool existe = File.ReadAllLines(rutaClientes)
                        .Any(linea => linea.Split(',')[0].Trim() == id);

                    if (existe)
                    {
                        MostrarMensaje("El ID del cliente ya existe. Intenta nuevamente.");
                        MostrarSiguienteId();
                        return;
                    }
                }

                string registro = $"{id},{nombre},{telefono},{correo}";

                using (StreamWriter escritor = new StreamWriter(
                    rutaClientes,
                    true
                ))
                {
                    escritor.WriteLine(registro);
                }

                int numeroActual = ObtenerNumeroId(id);

                File.WriteAllText(
                    rutaConsecutivo,
                    numeroActual.ToString()
                );

                // Primero se limpia el formulario y después se muestra
                // el mensaje, para que no se borre al instante.
                LimpiarFormulario();
                MostrarSiguienteId();

                MostrarMensaje(
                    $"Cliente {nombre} registrado correctamente con el ID {id}.",
                    false);
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al guardar el cliente: " + ex.Message);
            }
        }

        private int ObtenerNumeroId(string id)
        {
            Match coincidencia = Regex.Match(id, @"^C(\d+)$");

            if (coincidencia.Success &&
                int.TryParse(coincidencia.Groups[1].Value, out int numero))
            {
                return numero;
            }

            return 0;
        }

        private bool NombreValido(string nombre)
        {
            if (nombre != nombre.Trim())
                return false;

            if (nombre.Contains("  "))
                return false;

            return nombre.All(c => char.IsLetter(c) || c == ' ');
        }

        private bool CorreoValido(string correo)
        {
            if (string.IsNullOrWhiteSpace(correo))
                return false;

            // No permite espacios ni caracteres especiales.
            // Solamente letras, números, puntos y un único @.
            string patron = @"^[A-Za-z0-9.]+@[A-Za-z0-9]+(?:\.[A-Za-z0-9]+)+$";

            return Regex.IsMatch(correo, patron);
        }

        // Muestra un mensaje en la caja de avisos.
        // Verde cuando todo salió bien, rojo cuando hay un error.
        private void MostrarMensaje(string mensaje, bool esError = true)
        {
            TxtMensaje.Text = mensaje;

            if (esError)
            {
                PanelMensaje.Background = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 255, 232, 227));

                PanelMensaje.BorderBrush = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 245, 198, 189));

                TxtMensaje.Foreground = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 192, 57, 43));

                IconoMensaje.Glyph = "\uE7BA";

                IconoMensaje.Foreground = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 192, 57, 43));
            }
            else
            {
                PanelMensaje.Background = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 233, 245, 230));

                PanelMensaje.BorderBrush = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 195, 226, 189));

                TxtMensaje.Foreground = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 46, 107, 43));

                IconoMensaje.Glyph = "\uE73E";

                IconoMensaje.Foreground = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 46, 107, 43));
            }

            PanelMensaje.Visibility = Visibility.Visible;
        }

        private void LimpiarFormulario()
        {
            TxtNombreCliente.Text = string.Empty;
            TxtTelefono.Text = string.Empty;
            TxtCorreo.Text = string.Empty;

            CmbProveedorCorreo.SelectedIndex = 0;

            TxtMensaje.Text = string.Empty;
            PanelMensaje.Visibility = Visibility.Collapsed;
        }

        private void BtnIrMascotas_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Current?.SeleccionarPantalla("Mascotas");
        }

        private void TxtNombreCliente_TextChanging(
            TextBox sender,
            TextBoxTextChangingEventArgs args)
        {
            if (_validandoTexto)
                return;

            string textoOriginal = sender.Text;

            string textoFiltrado = new string(
                textoOriginal
                    .Where(c => char.IsLetter(c) || c == ' ')
                    .ToArray()
            );

            // Evita espacios al inicio y espacios dobles.
            textoFiltrado = Regex.Replace(textoFiltrado, @"^ +", "");
            textoFiltrado = Regex.Replace(textoFiltrado, @" {2,}", " ");

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

        private void TxtTelefono_TextChanging(
            TextBox sender,
            TextBoxTextChangingEventArgs args)
        {
            if (_validandoTexto)
                return;

            string textoOriginal = sender.Text;

            string textoFiltrado = new string(
                textoOriginal
                    .Where(char.IsDigit)
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

        private void TxtCorreo_TextChanging(
            TextBox sender,
            TextBoxTextChangingEventArgs args)
        {
            if (_validandoTexto || _actualizandoCorreo)
                return;

            string textoOriginal = sender.Text;
            int posicionOriginal = sender.SelectionStart;

            // Únicamente permite letras ASCII, números, punto y @.
            string textoFiltrado = new string(
                textoOriginal
                    .Where(c =>
                        (c >= 'A' && c <= 'Z') ||
                        (c >= 'a' && c <= 'z') ||
                        (c >= '0' && c <= '9') ||
                        c == '.' ||
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

        private void CmbProveedorCorreo_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (TxtCorreo == null || CmbProveedorCorreo.SelectedItem == null)
                return;

            string? proveedor =
                (CmbProveedorCorreo.SelectedItem as ComboBoxItem)?.Content?.ToString();

            if (string.IsNullOrWhiteSpace(proveedor) ||
                proveedor == "Automático")
                return;

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