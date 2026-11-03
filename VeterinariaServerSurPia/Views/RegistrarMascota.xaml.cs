using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace VeterinariaServerSurPia.Views
{
    public sealed partial class RegistrarMascota : Page
    {
        private sealed class ClienteOpcion
        {
            public string IDCliente { get; set; } = "";
            public string Nombre { get; set; } = "";
            public string TextoMostrar { get; set; } = "";
        }

        private readonly List<ClienteOpcion> clientes = new();

        private bool _filtrando = false;

        public RegistrarMascota()
        {
            InitializeComponent();

            CargarClientes();
            ActualizarIdMascota();
        }

        private string ObtenerRuta(string archivo)
        {
            return Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                archivo
            );
        }

        private string GenerarIdMascota()
        {
            string ruta = ObtenerRuta("mascotas.txt");
            int maximo = 0;

            if (File.Exists(ruta))
            {
                foreach (string linea in File.ReadLines(ruta))
                {
                    string[] datos = linea.Split(',');

                    if (datos.Length == 0)
                        continue;

                    string id = datos[0].Trim();

                    Match coincidencia = Regex.Match(
                        id,
                        @"^M(\d+)$",
                        RegexOptions.IgnoreCase
                    );

                    if (coincidencia.Success &&
                        int.TryParse(
                            coincidencia.Groups[1].Value,
                            out int numero))
                    {
                        maximo = Math.Max(maximo, numero);
                    }
                }
            }

            return "M" + (maximo + 1).ToString("D4");
        }

        private void ActualizarIdMascota()
        {
            try
            {
                TxtIdMascota.Text = GenerarIdMascota();
            }
            catch (Exception ex)
            {
                MostrarMensaje(
                    "No se pudo generar el ID de la mascota: " + ex.Message,
                    true
                );
            }
        }

        // avisar = true cuando el usuario presiona "Actualizar lista".
        // Al abrir la pantalla solo se avisa si no hay clientes.
        private void CargarClientes(bool avisar = false)
        {
            string? idAnterior =
                (CmbPropietario.SelectedItem as ClienteOpcion)?.IDCliente;

            clientes.Clear();

            string ruta = ObtenerRuta("clientes.txt");

            if (File.Exists(ruta))
            {
                foreach (string linea in File.ReadLines(ruta))
                {
                    string[] datos = linea.Split(',');

                    if (datos.Length >= 2 &&
                        !string.IsNullOrWhiteSpace(datos[0]) &&
                        !string.IsNullOrWhiteSpace(datos[1]))
                    {
                        string id = datos[0].Trim();
                        string nombre = datos[1].Trim();

                        clientes.Add(new ClienteOpcion
                        {
                            IDCliente = id,
                            Nombre = nombre,
                            TextoMostrar = $"{nombre} ({id})"
                        });
                    }
                }
            }

            CmbPropietario.ItemsSource = null;
            CmbPropietario.ItemsSource = clientes;

            ClienteOpcion? anterior = clientes.FirstOrDefault(
                c => c.IDCliente.Equals(
                    idAnterior,
                    StringComparison.OrdinalIgnoreCase
                )
            );

            CmbPropietario.SelectedItem = anterior;

            if (clientes.Count == 0)
            {
                MostrarMensaje(
                    "No hay clientes registrados. Registra uno primero.",
                    true
                );
            }
            else if (avisar)
            {
                MostrarMensaje(
                    "Clientes cargados. Selecciona un propietario.",
                    false
                );
            }
            else
            {
                OcultarMensaje();
            }
        }

        private void BtnActualizarClientes_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                CargarClientes(true);
            }
            catch (Exception ex)
            {
                MostrarMensaje(
                    "No se pudo actualizar la lista de clientes: " + ex.Message,
                    true
                );
            }
        }

        private void BtnGuardarMascota_Click(
            object sender,
            RoutedEventArgs e)
        {
            string nombre = TxtNombreMascota.Text.Trim();
            string raza = TxtRaza.Text.Trim();
            string edadTexto = TxtEdad.Text.Trim();
            string pesoTexto = TxtPeso.Text.Trim();

            string especie = CmbEspecie.SelectedItem?.ToString() ?? "";
            string unidad = CmbUnidadEdad.SelectedItem?.ToString() ?? "";
            string sexo = CmbSexo.SelectedItem?.ToString() ?? "";

            // Validación del nombre.
            if (string.IsNullOrWhiteSpace(nombre))
            {
                MostrarMensaje(
                    "Ingresa el nombre de la mascota.",
                    true
                );
                TxtNombreMascota.Focus(FocusState.Programmatic);
                return;
            }

            if (!TextoNombreValido(nombre) || nombre.Length > 50)
            {
                MostrarMensaje(
                    "El nombre solo debe contener letras y espacios, sin espacios dobles ni al inicio o al final.",
                    true
                );
                TxtNombreMascota.Focus(FocusState.Programmatic);
                return;
            }

            // Validación de la especie.
            if (string.IsNullOrWhiteSpace(especie))
            {
                MostrarMensaje(
                    "Selecciona la especie de la mascota.",
                    true
                );
                CmbEspecie.Focus(FocusState.Programmatic);
                return;
            }

            // Validación de la raza.
            if (raza.Length > 60 ||
                (raza.Length > 0 && !TextoRazaValido(raza)))
            {
                MostrarMensaje(
                    "La raza solo debe contener letras, espacios y guiones.",
                    true
                );
                TxtRaza.Focus(FocusState.Programmatic);
                return;
            }

            // Validación de la edad.
            if (string.IsNullOrWhiteSpace(edadTexto))
            {
                MostrarMensaje(
                    "Ingresa la edad de la mascota.",
                    true
                );
                TxtEdad.Focus(FocusState.Programmatic);
                return;
            }

            if (!Regex.IsMatch(edadTexto, @"^\d{1,3}$") ||
                !int.TryParse(edadTexto, out int edad))
            {
                MostrarMensaje(
                    "La edad debe contener únicamente números.",
                    true
                );
                TxtEdad.Focus(FocusState.Programmatic);
                return;
            }

            if (string.IsNullOrWhiteSpace(unidad))
            {
                MostrarMensaje(
                    "Selecciona si la edad está expresada en meses o años.",
                    true
                );
                CmbUnidadEdad.Focus(FocusState.Programmatic);
                return;
            }

            if (unidad == "Meses" && edad > 180)
            {
                MostrarMensaje(
                    "La edad en meses no puede superar los 180.",
                    true
                );
                TxtEdad.Focus(FocusState.Programmatic);
                return;
            }

            if (unidad == "Años" && edad > 150)
            {
                MostrarMensaje(
                    "La edad en años no puede superar los 150.",
                    true
                );
                TxtEdad.Focus(FocusState.Programmatic);
                return;
            }

            // Validación del sexo.
            if (string.IsNullOrWhiteSpace(sexo))
            {
                MostrarMensaje(
                    "Selecciona el sexo de la mascota.",
                    true
                );
                CmbSexo.Focus(FocusState.Programmatic);
                return;
            }

            // Validación del peso.
            if (string.IsNullOrWhiteSpace(pesoTexto))
            {
                MostrarMensaje(
                    "Ingresa el peso de la mascota.",
                    true
                );
                TxtPeso.Focus(FocusState.Programmatic);
                return;
            }

            if (!Regex.IsMatch(pesoTexto, @"^\d{1,5}(\.\d{1,2})?$") ||
                !decimal.TryParse(
                    pesoTexto,
                    NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out decimal peso))
            {
                MostrarMensaje(
                    "El peso debe contener números y, opcionalmente, un punto con máximo dos decimales.",
                    true
                );
                TxtPeso.Focus(FocusState.Programmatic);
                return;
            }

            if (peso <= 0 || peso > 1000)
            {
                MostrarMensaje(
                    "El peso debe ser mayor a 0 y no puede superar los 1000 kg.",
                    true
                );
                TxtPeso.Focus(FocusState.Programmatic);
                return;
            }

            // Validación del propietario.
            if (CmbPropietario.SelectedItem is not ClienteOpcion propietario)
            {
                MostrarMensaje(
                    "Selecciona un propietario registrado.",
                    true
                );
                CmbPropietario.Focus(FocusState.Programmatic);
                return;
            }

            try
            {
                string rutaClientes = ObtenerRuta("clientes.txt");
                string rutaMascotas = ObtenerRuta("mascotas.txt");

                bool clienteExiste = File.Exists(rutaClientes) &&
                    File.ReadLines(rutaClientes).Any(linea =>
                    {
                        string[] datos = linea.Split(',');

                        return datos.Length >= 2 &&
                            datos[0].Trim().Equals(
                                propietario.IDCliente,
                                StringComparison.OrdinalIgnoreCase
                            );
                    });

                if (!clienteExiste)
                {
                    CargarClientes();

                    MostrarMensaje(
                        "El propietario seleccionado ya no existe. Actualiza la lista de clientes.",
                        true
                    );
                    return;
                }

                // Generar nuevamente el ID antes de guardar.
                string id = GenerarIdMascota();

                string edadGuardada =
                    $"{edad} {unidad.ToLowerInvariant()}";

                string pesoGuardado =
                    peso.ToString(CultureInfo.InvariantCulture);

                string[] campos =
                {
                    id,
                    nombre,
                    especie,
                    raza,
                    edadGuardada,
                    sexo,
                    pesoGuardado,
                    propietario.IDCliente
                };

                // Evitar alterar la estructura CSV.
                if (campos.Any(c =>
                    c.Contains(',') ||
                    c.Contains('\n') ||
                    c.Contains('\r')))
                {
                    MostrarMensaje(
                        "Los datos contienen caracteres no permitidos.",
                        true
                    );
                    return;
                }

                string nuevaLinea = string.Join(",", campos);

                if (File.Exists(rutaMascotas) &&
                    new FileInfo(rutaMascotas).Length > 0)
                {
                    string contenido = File.ReadAllText(rutaMascotas);

                    if (!contenido.EndsWith("\n") &&
                        !contenido.EndsWith("\r"))
                    {
                        nuevaLinea =
                            Environment.NewLine + nuevaLinea;
                    }
                }

                File.AppendAllText(
                    rutaMascotas,
                    nuevaLinea + Environment.NewLine,
                    Encoding.UTF8
                );

                LimpiarFormulario();
                ActualizarIdMascota();

                MostrarMensaje(
                    $"Mascota {nombre} registrada correctamente con ID {id} y vinculada con {propietario.Nombre}.",
                    false
                );
            }
            catch (Exception ex)
            {
                MostrarMensaje(
                    "Error al guardar la mascota: " + ex.Message,
                    true
                );
            }
        }

        private bool TextoNombreValido(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return false;

            if (texto != texto.Trim())
                return false;

            if (texto.Contains("  "))
                return false;

            return texto.All(c =>
                char.IsLetter(c) || c == ' '
            );
        }

        private bool TextoRazaValido(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return false;

            if (texto != texto.Trim())
                return false;

            if (texto.Contains("  "))
                return false;

            return texto.All(c =>
                char.IsLetter(c) ||
                c == ' ' ||
                c == '-'
            );
        }

        // Muestra un mensaje en la caja de avisos.
        // Verde cuando todo salió bien, rojo cuando hay un error.
        private void MostrarMensaje(string texto, bool esError)
        {
            TxtMensaje.Text = texto;

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

        private void OcultarMensaje()
        {
            TxtMensaje.Text = string.Empty;
            PanelMensaje.Visibility = Visibility.Collapsed;
        }

        private void LimpiarFormulario()
        {
            TxtNombreMascota.Text = "";

            CmbEspecie.SelectedIndex = -1;

            TxtRaza.Text = "";

            TxtEdad.Text = "";

            CmbUnidadEdad.SelectedIndex = -1;

            CmbSexo.SelectedIndex = -1;

            TxtPeso.Text = "";

            CmbPropietario.SelectedIndex = -1;
        }

        private void BtnIrClientes_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (MainWindow.Current != null)
            {
                MainWindow.Current.MostrarMenuPrincipal();
                MainWindow.Current.SeleccionarPantalla("Clientes");
            }
        }

        // FILTRO DEL NOMBRE DE LA MASCOTA.
        private void TxtNombreMascota_TextChanging(
            TextBox sender,
            TextBoxTextChangingEventArgs args)
        {
            Filtrar(
                sender,
                c => char.IsLetter(c) || c == ' ',
                50,
                true
            );
        }

        // FILTRO DE LA RAZA.
        private void TxtRaza_TextChanging(
            TextBox sender,
            TextBoxTextChangingEventArgs args)
        {
            Filtrar(
                sender,
                c => char.IsLetter(c) || c == ' ' || c == '-',
                60,
                true
            );
        }

        // FILTRO DE LA EDAD.
        private void TxtEdad_TextChanging(
            TextBox sender,
            TextBoxTextChangingEventArgs args)
        {
            Filtrar(
                sender,
                c => c >= '0' && c <= '9',
                3
            );
        }

        // FILTRO DEL PESO.
        private void TxtPeso_TextChanging(
            TextBox sender,
            TextBoxTextChangingEventArgs args)
        {
            if (_filtrando)
                return;

            string original = sender.Text;
            int posicionOriginal = sender.SelectionStart;

            // Permitir que el usuario escriba coma o punto,
            // pero almacenar siempre el punto decimal.
            string texto = original.Replace(',', '.');

            string filtrado = "";
            bool tienePunto = false;
            int decimales = 0;

            foreach (char c in texto)
            {
                if (c >= '0' && c <= '9')
                {
                    if (tienePunto)
                    {
                        if (decimales >= 2)
                            continue;

                        decimales++;
                    }

                    filtrado += c;
                }
                else if (c == '.' && !tienePunto)
                {
                    tienePunto = true;
                    filtrado += c;
                }

                if (filtrado.Length >= 8)
                    break;
            }

            if (original == filtrado)
                return;

            int nuevaPosicion = original
                .Take(Math.Min(posicionOriginal, original.Length))
                .Count(c =>
                    (c >= '0' && c <= '9') ||
                    c == '.' ||
                    c == ','
                );

            nuevaPosicion = Math.Min(
                nuevaPosicion,
                filtrado.Length
            );

            try
            {
                _filtrando = true;

                sender.Text = filtrado;
                sender.SelectionStart = nuevaPosicion;
            }
            finally
            {
                _filtrando = false;
            }
        }

        // MÉTODO GENERAL PARA FILTRAR CARACTERES.
        private void Filtrar(
            TextBox caja,
            Func<char, bool> permitido,
            int maximo,
            bool evitarEspaciosDobles = false)
        {
            if (_filtrando)
                return;

            string original = caja.Text;
            int posicionOriginal = caja.SelectionStart;

            string filtrado = new string(
                original
                    .Where(permitido)
                    .Take(maximo)
                    .ToArray()
            );

            if (evitarEspaciosDobles)
            {
                filtrado = Regex.Replace(
                    filtrado,
                    @"^ +",
                    ""
                );

                filtrado = Regex.Replace(
                    filtrado,
                    @" {2,}",
                    " "
                );
            }

            if (original == filtrado)
                return;

            string prefijoOriginal = original.Substring(
                0,
                Math.Min(posicionOriginal, original.Length)
            );

            string prefijoFiltrado = new string(
                prefijoOriginal
                    .Where(permitido)
                    .ToArray()
            );

            if (evitarEspaciosDobles)
            {
                prefijoFiltrado = Regex.Replace(
                    prefijoFiltrado,
                    @"^ +",
                    ""
                );

                prefijoFiltrado = Regex.Replace(
                    prefijoFiltrado,
                    @" {2,}",
                    " "
                );
            }

            int nuevaPosicion = Math.Min(
                prefijoFiltrado.Length,
                filtrado.Length
            );

            try
            {
                _filtrando = true;

                caja.Text = filtrado;
                caja.SelectionStart = nuevaPosicion;
            }
            finally
            {
                _filtrando = false;
            }
        }
    }
}