
using Microsoft.Data.Sqlite;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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
    public sealed partial class RegistrarMascota : Page
    {
        private sealed class ClienteOpcion
        {
            public string IDCliente { get; set; } = "";
            public string Nombre { get; set; } = "";
            public string TextoMostrar { get; set; } = "";

            public override string ToString() => TextoMostrar;
        }

        private readonly List<ClienteOpcion> clientes = new();

        private bool _filtrando = false;
        private bool _guardandoMascota = false;

        public RegistrarMascota()
        {
            InitializeComponent();

            Loaded += RegistrarMascota_Loaded;
        }

        private async void RegistrarMascota_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            Loaded -= RegistrarMascota_Loaded;

            try
            {
                await CargarClientesAsync();
                await ActualizarIdMascotaAsync();
            }
            catch (Exception ex)
            {
                MostrarMensaje(
                    "No se pudieron cargar los datos: " + ex.Message,
                    true
                );
            }
        }

        private async Task<string> GenerarIdMascotaAsync()
        {
            using SqliteConnection conexion =
                await App.Database.OpenConnectionAsync();

            return await GenerarIdMascotaAsync(conexion, null);
        }

        private async Task<string> GenerarIdMascotaAsync(
            SqliteConnection conexion,
            SqliteTransaction? transaccion)
        {
            using SqliteCommand comando = conexion.CreateCommand();

            comando.Transaction = transaccion;

            comando.CommandText = @"
                SELECT IdMascota
                FROM Mascotas;
            ";

            int maximo = 0;

            using SqliteDataReader reader =
                await comando.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                string id = reader.IsDBNull(0)
                    ? ""
                    : reader.GetString(0).Trim();

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

            return "M" + (maximo + 1).ToString("D4");
        }

        private async Task ActualizarIdMascotaAsync()
        {
            try
            {
                TxtIdMascota.Text = await GenerarIdMascotaAsync();
            }
            catch (Exception ex)
            {
                TxtIdMascota.Text = "";

                MostrarMensaje(
                    "No se pudo generar el ID de la mascota: " + ex.Message,
                    true
                );
            }
        }

        private async Task CargarClientesAsync(bool avisar = false)
        {
            string? idAnterior =
                (CmbPropietario.SelectedItem as ClienteOpcion)?.IDCliente;

            clientes.Clear();

            using SqliteConnection conexion =
                await App.Database.OpenConnectionAsync();

            using SqliteCommand comando = conexion.CreateCommand();

            comando.CommandText = @"
                SELECT IdCliente, Nombre
                FROM Clientes
                WHERE Activo = 1
                ORDER BY Nombre;
            ";

            using SqliteDataReader reader =
                await comando.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                string id = reader.IsDBNull(0)
                    ? ""
                    : reader.GetString(0);

                string nombre = reader.IsDBNull(1)
                    ? ""
                    : reader.GetString(1);

                if (string.IsNullOrWhiteSpace(id) ||
                    string.IsNullOrWhiteSpace(nombre))
                {
                    continue;
                }

                clientes.Add(new ClienteOpcion
                {
                    IDCliente = id.Trim(),
                    Nombre = nombre.Trim(),
                    TextoMostrar = $"{nombre.Trim()} ({id.Trim()})"
                });
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

        private async void BtnActualizarClientes_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                await CargarClientesAsync(true);
            }
            catch (Exception ex)
            {
                MostrarMensaje(
                    "No se pudo actualizar la lista de clientes: " +
                    ex.Message,
                    true
                );
            }
        }

        private async void BtnGuardarMascota_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_guardandoMascota)
                return;

            string nombre = TxtNombreMascota.Text.Trim();
            string raza = TxtRaza.Text.Trim();
            string edadTexto = TxtEdad.Text.Trim();
            string pesoTexto = TxtPeso.Text.Trim();

            string especie =
                CmbEspecie.SelectedItem?.ToString() ?? "";

            string unidad =
                CmbUnidadEdad.SelectedItem?.ToString() ?? "";

            string sexo =
                CmbSexo.SelectedItem?.ToString() ?? "";

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

            if (string.IsNullOrWhiteSpace(especie))
            {
                MostrarMensaje(
                    "Selecciona la especie de la mascota.",
                    true
                );

                CmbEspecie.Focus(FocusState.Programmatic);
                return;
            }

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

            if (string.IsNullOrWhiteSpace(sexo))
            {
                MostrarMensaje(
                    "Selecciona el sexo de la mascota.",
                    true
                );

                CmbSexo.Focus(FocusState.Programmatic);
                return;
            }

            if (string.IsNullOrWhiteSpace(pesoTexto))
            {
                MostrarMensaje(
                    "Ingresa el peso de la mascota.",
                    true
                );

                TxtPeso.Focus(FocusState.Programmatic);
                return;
            }

            if (!Regex.IsMatch(
                    pesoTexto,
                    @"^\d{1,5}(\.\d{1,2})?$") ||
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

            if (CmbPropietario.SelectedItem is not ClienteOpcion propietario)
            {
                MostrarMensaje(
                    "Selecciona un propietario registrado.",
                    true
                );

                CmbPropietario.Focus(FocusState.Programmatic);
                return;
            }

            _guardandoMascota = true;

            if (sender is Button botonGuardar)
                botonGuardar.IsEnabled = false;

            try
            {
                using SqliteConnection conexion =
                    await App.Database.OpenConnectionAsync();

                using SqliteTransaction transaccion =
                    conexion.BeginTransaction();

                using (SqliteCommand verificarCliente = conexion.CreateCommand())
                {
                    verificarCliente.Transaction = transaccion;

                    verificarCliente.CommandText = @"
                        SELECT COUNT(*)
                        FROM Clientes
                        WHERE IdCliente = $id
                          AND Activo = 1;
                    ";

                    verificarCliente.Parameters.AddWithValue(
                        "$id",
                        propietario.IDCliente
                    );

                    object? resultado =
                        await verificarCliente.ExecuteScalarAsync();

                    long existe = Convert.ToInt64(
                        resultado ?? 0L,
                        CultureInfo.InvariantCulture
                    );

                    if (existe == 0)
                    {
                        transaccion.Rollback();

                        await CargarClientesAsync();

                        MostrarMensaje(
                            "El propietario seleccionado ya no existe o está inactivo. Actualiza la lista de clientes.",
                            true
                        );

                        return;
                    }
                }

                string id = await GenerarIdMascotaAsync(
                    conexion,
                    transaccion
                );

                string edadGuardada =
                    $"{edad} {unidad.ToLowerInvariant()}";

                using SqliteCommand guardar = conexion.CreateCommand();

                guardar.Transaction = transaccion;

                guardar.CommandText = @"
                    INSERT INTO Mascotas
                    (
                        IdMascota,
                        Nombre,
                        Especie,
                        Raza,
                        Edad,
                        Sexo,
                        Peso,
                        IdCliente
                    )
                    VALUES
                    (
                        $id,
                        $nombre,
                        $especie,
                        $raza,
                        $edad,
                        $sexo,
                        $peso,
                        $idCliente
                    );
                ";

                guardar.Parameters.AddWithValue("$id", id);
                guardar.Parameters.AddWithValue("$nombre", nombre);
                guardar.Parameters.AddWithValue("$especie", especie);
                guardar.Parameters.AddWithValue("$raza", raza);
                guardar.Parameters.AddWithValue("$edad", edadGuardada);
                guardar.Parameters.AddWithValue("$sexo", sexo);
                guardar.Parameters.AddWithValue("$peso", (double)peso);
                guardar.Parameters.AddWithValue(
                    "$idCliente",
                    propietario.IDCliente
                );

                await guardar.ExecuteNonQueryAsync();

                transaccion.Commit();

                LimpiarFormulario();

                await ActualizarIdMascotaAsync();

                MostrarMensaje(
                    $"Mascota {nombre} registrada correctamente con ID {id} y vinculada con {propietario.Nombre}.",
                    false
                );
            }
            catch (SqliteException ex)
            {
                MostrarMensaje(
                    "Error de base de datos al guardar la mascota: " +
                    ex.Message,
                    true
                );
            }
            catch (Exception ex)
            {
                MostrarMensaje(
                    "Error al guardar la mascota: " + ex.Message,
                    true
                );
            }
            finally
            {
                _guardandoMascota = false;

                if (sender is Button boton)
                    boton.IsEnabled = true;
            }
        }

        private bool TextoNombreValido(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto) ||
                texto != texto.Trim() ||
                texto.Contains("  "))
            {
                return false;
            }

            return texto.All(c =>
                char.IsLetter(c) || c == ' '
            );
        }

        private bool TextoRazaValido(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto) ||
                texto != texto.Trim() ||
                texto.Contains("  "))
            {
                return false;
            }

            return texto.All(c =>
                char.IsLetter(c) ||
                c == ' ' ||
                c == '-'
            );
        }

        private void MostrarMensaje(
            string texto,
            bool esError)
        {
            TxtMensaje.Text = texto;

            if (esError)
            {
                PanelMensaje.Background = new SolidColorBrush(
                    Color.FromArgb(255, 255, 232, 227)
                );

                PanelMensaje.BorderBrush = new SolidColorBrush(
                    Color.FromArgb(255, 245, 198, 189)
                );

                TxtMensaje.Foreground = new SolidColorBrush(
                    Color.FromArgb(255, 192, 57, 43)
                );

                IconoMensaje.Glyph = "\uE7BA";

                IconoMensaje.Foreground = new SolidColorBrush(
                    Color.FromArgb(255, 192, 57, 43)
                );
            }
            else
            {
                PanelMensaje.Background = new SolidColorBrush(
                    Color.FromArgb(255, 233, 245, 230)
                );

                PanelMensaje.BorderBrush = new SolidColorBrush(
                    Color.FromArgb(255, 195, 226, 189)
                );

                TxtMensaje.Foreground = new SolidColorBrush(
                    Color.FromArgb(255, 46, 107, 43)
                );

                IconoMensaje.Glyph = "\uE73E";

                IconoMensaje.Foreground = new SolidColorBrush(
                    Color.FromArgb(255, 46, 107, 43)
                );
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

        private void TxtPeso_TextChanging(
            TextBox sender,
            TextBoxTextChangingEventArgs args)
        {
            if (_filtrando)
                return;

            string original = sender.Text;
            int posicionOriginal = sender.SelectionStart;

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
