
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace VeterinariaServerSurPia.Views
{
    public sealed partial class RegistrarCliente : Page
    {
        private bool _validandoTexto = false;

        private readonly string rutaClientes = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "clientes.txt");

        private readonly string rutaConsecutivo = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "ultimo_id_cliente.txt");

        public RegistrarCliente()
        {
            this.InitializeComponent();

            // El veterinario no puede modificar el ID.
            TxtIdCliente.IsReadOnly = true;
            TxtIdCliente.IsTabStop = false;

            // Generar el ID al abrir el formulario.
            MostrarSiguienteId();
        }

        // GENERAR EL SIGUIENTE NÚMERO
        private int ObtenerSiguienteNumero()
        {
            int ultimoNumero = 0;

            // Consultar el último ID utilizado.
            if (File.Exists(rutaConsecutivo))
            {
                string contenido = File.ReadAllText(
                    rutaConsecutivo).Trim();

                if (int.TryParse(contenido, out int numero))
                {
                    if (numero > ultimoNumero)
                        ultimoNumero = numero;
                }
            }

            // Consultar IDs que ya existen.
            if (File.Exists(rutaClientes))
            {
                foreach (string linea in File.ReadLines(rutaClientes))
                {
                    if (string.IsNullOrWhiteSpace(linea))
                        continue;

                    string id = linea.Split(',')[0].Trim();

                    Match resultado = Regex.Match(
                        id,
                        @"^C(\d+)$",
                        RegexOptions.IgnoreCase);

                    if (resultado.Success &&
                        int.TryParse(
                            resultado.Groups[1].Value,
                            out int numero))
                    {
                        if (numero > ultimoNumero)
                            ultimoNumero = numero;
                    }
                }
            }

            if (ultimoNumero == int.MaxValue)
            {
                throw new InvalidOperationException(
                    "Se alcanzó el límite de IDs.");
            }

            return ultimoNumero + 1;
        }

        // MOSTRAR ID AUTOMÁTICO
        private void MostrarSiguienteId()
        {
            try
            {
                int numero = ObtenerSiguienteNumero();

                TxtIdCliente.Text = $"C{numero:D4}";
            }
            catch (Exception ex)
            {
                TxtIdCliente.Text = "";

                MostrarMensaje(
                    "Error al generar el ID: " + ex.Message,
                    true);
            }
        }

        // GUARDAR CLIENTE
        private void BtnGuardarCliente_Click(
            object sender,
            RoutedEventArgs e)
        {
            string nombre = TxtNombreCliente.Text.Trim();
            string telefono = TxtTelefono.Text.Trim();
            string correo = TxtCorreo.Text.Trim();

            // VALIDAR NOMBRE
            if (string.IsNullOrWhiteSpace(nombre))
            {
                MostrarMensaje(
                    "El nombre completo es obligatorio.",
                    true);
                return;
            }

            if (nombre.Length > 80 ||
                !nombre.All(c => char.IsLetter(c) || c == ' '))
            {
                MostrarMensaje(
                    "El nombre solo permite letras y espacios.",
                    true);
                return;
            }

            // VALIDAR CONTACTO OBLIGATORIO
            if (string.IsNullOrWhiteSpace(telefono) &&
                string.IsNullOrWhiteSpace(correo))
            {
                MostrarMensaje(
                    "Debes ingresar al menos un teléfono o correo.",
                    true);
                return;
            }

            // VALIDAR TELÉFONO
            if (!string.IsNullOrWhiteSpace(telefono) &&
                !Regex.IsMatch(telefono, @"^[0-9]{10}$"))
            {
                MostrarMensaje(
                    "El teléfono debe contener exactamente 10 dígitos.",
                    true);
                return;
            }

            // VALIDAR CORREO
            if (!string.IsNullOrWhiteSpace(correo))
            {
                string patronCorreo =
                    @"^[A-Za-z0-9._%+-]+@(gmail\.com|outlook\.com|hotmail\.com|yahoo\.com|live\.com)$";

                if (!Regex.IsMatch(
                    correo,
                    patronCorreo,
                    RegexOptions.IgnoreCase))
                {
                    MostrarMensaje(
                        "Correo no válido. Se permiten Gmail, Outlook, Hotmail, Yahoo y Live.",
                        true);
                    return;
                }
            }

            // GUARDAR INFORMACIÓN
            try
            {
                int numero = ObtenerSiguienteNumero();
                string idCliente = $"C{numero:D4}";

                // Verificar que no exista el ID.
                if (File.Exists(rutaClientes))
                {
                    bool duplicado = File.ReadLines(rutaClientes)
                        .Any(linea =>
                            linea.Split(',')[0].Trim().Equals(
                                idCliente,
                                StringComparison.OrdinalIgnoreCase));

                    if (duplicado)
                    {
                        MostrarMensaje(
                            "El ID ya existe. Intenta nuevamente.",
                            true);
                        return;
                    }
                }

                // Mantener el formato de clientes.txt.
                string nuevaLinea =
                    $"{idCliente},{nombre},{telefono},{correo}";

                // Agregar salto de línea si es necesario.
                if (File.Exists(rutaClientes) &&
                    new FileInfo(rutaClientes).Length > 0)
                {
                    string contenido = File.ReadAllText(
                        rutaClientes);

                    if (!contenido.EndsWith("\n") &&
                        !contenido.EndsWith("\r"))
                    {
                        nuevaLinea =
                            Environment.NewLine + nuevaLinea;
                    }
                }

                File.AppendAllText(
                    rutaClientes,
                    nuevaLinea + Environment.NewLine);

                // Guardar último consecutivo.
                File.WriteAllText(
                    rutaConsecutivo,
                    numero.ToString());

                // Limpiar formulario.
                LimpiarFormulario();

                // Preparar siguiente ID.
                MostrarSiguienteId();

                MostrarMensaje(
                    $"¡Cliente registrado correctamente! ID: {idCliente}",
                    false);
            }
            catch (Exception ex)
            {
                MostrarMensaje(
                    "Error al guardar: " + ex.Message,
                    true);
            }
        }

        // MOSTRAR MENSAJES
        private void MostrarMensaje(
            string texto,
            bool esError)
        {
            TxtMensaje.Text = texto;

            TxtMensaje.Foreground = esError
                ? new SolidColorBrush(Colors.Red)
                : new SolidColorBrush(Colors.ForestGreen);

            TxtMensaje.Visibility = Visibility.Visible;
        }

        // LIMPIAR FORMULARIO
        private void LimpiarFormulario()
        {
            TxtNombreCliente.Text = "";
            TxtTelefono.Text = "";
            TxtCorreo.Text = "";
        }

        // IR A MASCOTAS
        private void BtnIrMascotas_Click(
            object sender,
            RoutedEventArgs e)
        {
            MainWindow.Current?.SeleccionarPantalla(
                "Mascotas");
        }

        // ID AUTOMÁTICO: NO SE PERMITE EDITAR
        private void TxtIdCliente_TextChanging(
            TextBox sender,
            TextBoxTextChangingEventArgs args)
        {
            // Sin filtrado: el ID se genera automáticamente.
        }

        // VALIDACIÓN DE NOMBRE
        private void TxtNombreCliente_TextChanging(
            TextBox sender,
            TextBoxTextChangingEventArgs args)
        {
            FiltrarCaracteres(
                sender,
                c => char.IsLetter(c) || c == ' ',
                80);
        }

        // VALIDACIÓN DE TELÉFONO
        private void TxtTelefono_TextChanging(
            TextBox sender,
            TextBoxTextChangingEventArgs args)
        {
            FiltrarCaracteres(
                sender,
                c => c >= '0' && c <= '9',
                10);
        }

        // FILTRAR CARACTERES INVÁLIDOS
        private void FiltrarCaracteres(
            TextBox caja,
            Func<char, bool> permitido,
            int longitudMaxima)
        {
            if (_validandoTexto)
                return;

            string textoOriginal = caja.Text;
            int posicionOriginal = caja.SelectionStart;

            string textoFiltrado = new string(
                textoOriginal
                    .Where(permitido)
                    .Take(longitudMaxima)
                    .ToArray());

            if (textoOriginal == textoFiltrado)
                return;

            int nuevaPosicion = textoOriginal
                .Take(posicionOriginal)
                .Count(permitido);

            nuevaPosicion = Math.Min(
                nuevaPosicion,
                textoFiltrado.Length);

            try
            {
                _validandoTexto = true;

                caja.Text = textoFiltrado;
                caja.SelectionStart = nuevaPosicion;
            }
            finally
            {
                _validandoTexto = false;
            }
        }
    }
}


