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
                AppDomain.CurrentDomain.BaseDirectory, archivo);
        }

        private string GenerarIdMascota()
        {
            string ruta = ObtenerRuta("mascotas.txt");
            int maximo = 0;

            if (File.Exists(ruta))
            {
                foreach (string linea in File.ReadLines(ruta))
                {
                    string id = linea.Split(',')[0].Trim();

                    Match coincidencia = Regex.Match(
                        id, @"^M(\d+)$",
                        RegexOptions.IgnoreCase);

                    if (coincidencia.Success &&
                        int.TryParse(coincidencia.Groups[1].Value,
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
            TxtIdMascota.Text = GenerarIdMascota();
        }

        private void CargarClientes()
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
                    StringComparison.OrdinalIgnoreCase));

            CmbPropietario.SelectedItem = anterior;

            MostrarMensaje(
                clientes.Count > 0
                    ? "Clientes cargados. Selecciona un propietario."
                    : "No hay clientes registrados. Registra uno primero.",
                clientes.Count == 0);
        }

        private void BtnActualizarClientes_Click(
            object sender, RoutedEventArgs e)
        {
            CargarClientes();
        }

        private void BtnGuardarMascota_Click(
            object sender, RoutedEventArgs e)
        {
            string nombre = TxtNombreMascota.Text.Trim();
            string raza = TxtRaza.Text.Trim();
            string edadTexto = TxtEdad.Text.Trim();
            string pesoTexto = TxtPeso.Text.Trim();

            string especie = CmbEspecie.SelectedItem?.ToString() ?? "";
            string unidad = CmbUnidadEdad.SelectedItem?.ToString() ?? "";
            string sexo = CmbSexo.SelectedItem?.ToString() ?? "";

            if (!Regex.IsMatch(nombre, @"^[\p{L} ]{1,50}$") ||
                string.IsNullOrWhiteSpace(nombre))
            {
                MostrarMensaje(
                    "Ingresa un nombre válido, solo letras y espacios.",
                    true);
                return;
            }

            if (string.IsNullOrWhiteSpace(especie))
            {
                MostrarMensaje("Selecciona la especie.", true);
                return;
            }

            if (raza.Length > 60 ||
                (raza.Length > 0 &&
                 !Regex.IsMatch(raza, @"^[\p{L} -]+$")))
            {
                MostrarMensaje("La raza contiene caracteres inválidos.", true);
                return;
            }

            if (!int.TryParse(edadTexto, out int edad) ||
                edad < 0 || edad > 150 ||
                string.IsNullOrWhiteSpace(unidad))
            {
                MostrarMensaje(
                    "Ingresa una edad válida y selecciona meses o años.",
                    true);
                return;
            }

            if (unidad == "Meses" && edad > 180)
            {
                MostrarMensaje("La edad en meses es demasiado alta.", true);
                return;
            }

            if (unidad == "Años" && edad > 150)
            {
                MostrarMensaje("La edad en años es demasiado alta.", true);
                return;
            }

            if (string.IsNullOrWhiteSpace(sexo))
            {
                MostrarMensaje("Selecciona el sexo.", true);
                return;
            }

            if (!decimal.TryParse(
                    pesoTexto,
                    NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out decimal peso) ||
                peso <= 0 || peso > 1000)
            {
                MostrarMensaje(
                    "Ingresa un peso válido mayor a 0 y máximo 1000 kg.",
                    true);
                return;
            }

            if (CmbPropietario.SelectedItem is not ClienteOpcion propietario)
            {
                MostrarMensaje(
                    "Selecciona un propietario registrado.", true);
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
                                StringComparison.OrdinalIgnoreCase);
                    });

                if (!clienteExiste)
                {
                    CargarClientes();
                    MostrarMensaje(
                        "El propietario ya no existe. Actualiza la lista.",
                        true);
                    return;
                }

                // Volvemos a calcular el ID antes de guardar.
                string id = GenerarIdMascota();

                string edadGuardada = $"{edad} {unidad.ToLowerInvariant()}";
                string pesoGuardado =
                    peso.ToString(CultureInfo.InvariantCulture);

                string[] campos =
                {
                    id, nombre, especie, raza,
                    edadGuardada, sexo, pesoGuardado,
                    propietario.IDCliente
                };

                // Compatibilidad con el formato CSV actual.
                if (campos.Any(c =>
                    c.Contains(',') || c.Contains('\n') || c.Contains('\r')))
                {
                    MostrarMensaje(
                        "Los datos contienen caracteres no permitidos.",
                        true);
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
                    Encoding.UTF8);

                LimpiarFormulario();
                ActualizarIdMascota();

                MostrarMensaje(
                    $"Mascota {nombre} registrada con ID {id} y vinculada con {propietario.Nombre}.",
                    false);
            }
            catch (Exception ex)
            {
                MostrarMensaje(
                    $"Error al guardar: {ex.Message}", true);
            }
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
            TxtNombreMascota.Text = "";
            CmbEspecie.SelectedIndex = -1;
            TxtRaza.Text = "";
            TxtEdad.Text = "";
            CmbUnidadEdad.SelectedIndex = -1;
            CmbSexo.SelectedIndex = -1;
            TxtPeso.Text = "";

          
        }

        private void BtnIrClientes_Click(
            object sender, RoutedEventArgs e)
        {
            if (MainWindow.Current != null)
            {
                MainWindow.Current.MostrarMenuPrincipal();
                MainWindow.Current.SeleccionarPantalla("Clientes");
            }
        }

        private void TxtNombreMascota_TextChanging(
            TextBox sender, TextBoxTextChangingEventArgs args)
        {
            Filtrar(sender,
                c => char.IsLetter(c) || c == ' ', 50);
        }

        private void TxtRaza_TextChanging(
            TextBox sender, TextBoxTextChangingEventArgs args)
        {
            Filtrar(sender,
                c => char.IsLetter(c) || c == ' ' || c == '-', 60);
        }

        private void TxtEdad_TextChanging(
            TextBox sender, TextBoxTextChangingEventArgs args)
        {
            Filtrar(sender,
                c => c >= '0' && c <= '9', 3);
        }

        private void TxtPeso_TextChanging(
            TextBox sender, TextBoxTextChangingEventArgs args)
        {
            if (_filtrando) return;

            string texto = sender.Text.Replace(',', '.');
            int posicion = sender.SelectionStart;

            string filtrado = "";
            bool tienePunto = false;
            int decimales = 0;

            foreach (char c in texto)
            {
                if (filtrado.Length >= 8) break;

                if (c >= '0' && c <= '9')
                {
                    if (tienePunto)
                    {
                        if (decimales >= 2) continue;
                        decimales++;
                    }
                    filtrado += c;
                }
                else if (c == '.' && !tienePunto)
                {
                    tienePunto = true;
                    filtrado += c;
                }
            }

            if (sender.Text == filtrado) return;

            try
            {
                _filtrando = true;
                sender.Text = filtrado;
                sender.SelectionStart =
                    Math.Min(posicion, filtrado.Length);
            }
            finally
            {
                _filtrando = false;
            }
        }

        private void Filtrar(
            TextBox caja,
            Func<char, bool> permitido,
            int maximo)
        {
            if (_filtrando) return;

            string original = caja.Text;
            int posicion = caja.SelectionStart;

            string filtrado = new string(
                original.Where(permitido)
                        .Take(maximo)
                        .ToArray());

            if (original == filtrado) return;

            int nuevaPosicion = Math.Min(
                original.Take(posicion).Count(permitido),
                filtrado.Length);

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
