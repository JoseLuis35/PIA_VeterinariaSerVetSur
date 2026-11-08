
using Microsoft.Data.Sqlite;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace VeterinariaServerSurPia.Views
{
    public class Cliente
    {
        public string IDCliente { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string Telefono { get; set; } = "";
        public string Correo { get; set; } = "";
    }

    public class Mascota
    {
        public string IDMascota { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string Especie { get; set; } = "";
        public string Raza { get; set; } = "";
        public string Edad { get; set; } = "";
        public string Sexo { get; set; } = "";
        public string Peso { get; set; } = "";
        public string IDCliente { get; set; } = "";
        public string NombrePropietario { get; set; } = "";
    }

    public class Cita
    {
        public string IDCita { get; set; } = "";
        public string IDMascota { get; set; } = "";
        public string NombreMascota { get; set; } = "";
        public string NombrePropietario { get; set; } = "";
        public string Fecha { get; set; } = "";
        public string Hora { get; set; } = "";
        public string Servicio { get; set; } = "";
        public string Observaciones { get; set; } = "";
    }

    public sealed partial class BusquedaConsulta : Page
    {
        private readonly List<Cliente> todosClientes = new();
        private readonly List<Mascota> todasMascotas = new();
        private readonly List<Cita> todasCitas = new();

        private bool _datosCargados = false;
        private bool _actualizandoDatos = false;
        private bool _inicializando = true;

        public BusquedaConsulta()
        {
            InitializeComponent();

            CategoriaCombo.SelectedIndex = 0;
            FiltroServicioCombo.SelectedIndex = 0;

            Loaded += BusquedaConsulta_Loaded;
        }

        // =========================================================
        // OBTENER CONTROLES MEDIANTE FINDNAME
        // =========================================================

        private Button ObtenerBotonActualizar()
        {
            return FindName("ActualizarBtn") as Button;
        }

        private Button ObtenerBotonBuscar()
        {
            return FindName("BuscarBtn") as Button;
        }

        private TextBox ObtenerCampoBusqueda()
        {
            return FindName("BusquedaText") as TextBox;
        }

        // =========================================================
        // CARGA INICIAL
        // =========================================================

        private async void BusquedaConsulta_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            Loaded -= BusquedaConsulta_Loaded;

            try
            {
                MostrarEstado("Cargando registros...", false);

                await CargarDatosAsync();

                _datosCargados = true;
                _inicializando = false;

                AplicarBusqueda();
            }
            catch (Exception ex)
            {
                _datosCargados = false;
                _inicializando = false;

                MostrarEstado(
                    "No se pudieron cargar los registros: " + ex.Message,
                    true);
            }
        }

        // =========================================================
        // LECTURA SEGURA DE SQLITE
        // =========================================================

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

        // =========================================================
        // CARGAR INFORMACIÓN DESDE SQLITE
        // =========================================================

        private async Task CargarDatosAsync()
        {
            todosClientes.Clear();
            todasMascotas.Clear();
            todasCitas.Clear();

            using SqliteConnection conexion =
                await App.Database.OpenConnectionAsync();

            // CLIENTES

            using (SqliteCommand comandoClientes =
                conexion.CreateCommand())
            {
                comandoClientes.CommandText = @"
                    SELECT
                        IdCliente,
                        Nombre,
                        Telefono,
                        Correo
                    FROM Clientes
                    WHERE Activo = 1
                    ORDER BY Nombre;";

                using SqliteDataReader reader =
                    await comandoClientes.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    todosClientes.Add(new Cliente
                    {
                        IDCliente = LeerTexto(reader, 0),
                        Nombre = LeerTexto(reader, 1),
                        Telefono = LeerTexto(reader, 2),
                        Correo = LeerTexto(reader, 3)
                    });
                }
            }

            // MASCOTAS Y PROPIETARIOS

            using (SqliteCommand comandoMascotas =
                conexion.CreateCommand())
            {
                comandoMascotas.CommandText = @"
                    SELECT
                        m.IdMascota,
                        m.Nombre,
                        m.Especie,
                        m.Raza,
                        m.Edad,
                        m.Sexo,
                        m.Peso,
                        m.IdCliente,
                        COALESCE(c.Nombre, 'No encontrado')
                    FROM Mascotas m
                    LEFT JOIN Clientes c
                        ON c.IdCliente = m.IdCliente
                    ORDER BY m.Nombre;";

                using SqliteDataReader reader =
                    await comandoMascotas.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    todasMascotas.Add(new Mascota
                    {
                        IDMascota = LeerTexto(reader, 0),
                        Nombre = LeerTexto(reader, 1),
                        Especie = LeerTexto(reader, 2),
                        Raza = LeerTexto(reader, 3),
                        Edad = LeerTexto(reader, 4),
                        Sexo = LeerTexto(reader, 5),
                        Peso = LeerTexto(reader, 6),
                        IDCliente = LeerTexto(reader, 7),
                        NombrePropietario = LeerTexto(reader, 8)
                    });
                }
            }

            // CITAS, MASCOTAS Y PROPIETARIOS

            using (SqliteCommand comandoCitas =
                conexion.CreateCommand())
            {
                comandoCitas.CommandText = @"
                    SELECT
                        ci.IdCita,
                        ci.IdMascota,
                        COALESCE(m.Nombre, 'No encontrada'),
                        COALESCE(c.Nombre, 'No encontrado'),
                        ci.Fecha,
                        ci.Hora,
                        ci.Servicio,
                        ci.Observaciones
                    FROM Citas ci
                    LEFT JOIN Mascotas m
                        ON m.IdMascota = ci.IdMascota
                    LEFT JOIN Clientes c
                        ON c.IdCliente = m.IdCliente
                    ORDER BY ci.Fecha, ci.Hora;";

                using SqliteDataReader reader =
                    await comandoCitas.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    todasCitas.Add(new Cita
                    {
                        IDCita = LeerTexto(reader, 0),
                        IDMascota = LeerTexto(reader, 1),
                        NombreMascota = LeerTexto(reader, 2),
                        NombrePropietario = LeerTexto(reader, 3),
                        Fecha = LeerTexto(reader, 4),
                        Hora = LeerTexto(reader, 5),
                        Servicio = LeerTexto(reader, 6),
                        Observaciones = LeerTexto(reader, 7)
                    });
                }
            }
        }

        // =========================================================
        // VALIDACIÓN DEL BUSCADOR
        // =========================================================

        // Término de búsqueda: solo letras y números, sin espacios.
        // Excepciones según el criterio elegido, porque lo necesitan para
        // poder buscar: correo (@ . _ -) y fecha (/ -).
        private bool CaracterPermitidoBusqueda(char caracter)
        {
            if (char.IsLetterOrDigit(caracter))
                return true;

            string criterio =
                CriterioCombo?.SelectedItem?.ToString() ?? "";

            return criterio switch
            {
                "Por correo" => caracter is '@' or '.' or '_' or '-',
                "Por fecha" => caracter is '/' or '-',
                _ => false
            };
        }

        private void BusquedaText_BeforeTextChanging(
            TextBox sender,
            TextBoxBeforeTextChangingEventArgs args)
        {
            foreach (char caracter in args.NewText)
            {
                if (!CaracterPermitidoBusqueda(caracter))
                {
                    args.Cancel = true;
                    return;
                }
            }
        }

        private void BusquedaText_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            if (!_datosCargados || _inicializando)
                return;

            if (sender is not TextBox textBox)
                return;

            if (textBox.Text.Length > 100)
            {
                textBox.Text = textBox.Text.Substring(0, 100);
                textBox.SelectionStart = textBox.Text.Length;
            }

            AplicarBusqueda();
        }

        // =========================================================
        // CAMBIO DE CATEGORÍA
        // =========================================================

        private void CategoriaCombo_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            TextBox campoBusqueda = ObtenerCampoBusqueda();

            if (CriterioCombo == null ||
                campoBusqueda == null ||
                FiltroServicioCombo == null ||
                ScrollClientes == null ||
                ScrollMascotas == null ||
                ScrollCitas == null)
            {
                return;
            }

            CriterioCombo.Items.Clear();
            campoBusqueda.Text = "";

            FiltroServicioCombo.Visibility =
                Visibility.Collapsed;

            ScrollClientes.Visibility =
                Visibility.Collapsed;

            ScrollMascotas.Visibility =
                Visibility.Collapsed;

            ScrollCitas.Visibility =
                Visibility.Collapsed;

            string categoria =
                CategoriaCombo.SelectedItem?.ToString() ?? "";

            if (categoria == "Clientes")
            {
                AgregarCriterios(
                    "Por ID",
                    "Por nombre",
                    "Por teléfono",
                    "Por correo");

                ScrollClientes.Visibility =
                    Visibility.Visible;
            }
            else if (categoria == "Mascotas")
            {
                AgregarCriterios(
                    "Por ID",
                    "Por nombre",
                    "Por especie",
                    "Por raza",
                    "Por propietario");

                ScrollMascotas.Visibility =
                    Visibility.Visible;
            }
            else if (categoria == "Citas")
            {
                AgregarCriterios(
                    "Por ID de cita",
                    "Por fecha",
                    "Por tipo de servicio",
                    "Por mascota",
                    "Por propietario");

                FiltroServicioCombo.Visibility =
                    Visibility.Visible;

                FiltroServicioCombo.SelectedIndex = 0;

                ScrollCitas.Visibility =
                    Visibility.Visible;
            }

            if (CriterioCombo.Items.Count > 0)
                CriterioCombo.SelectedIndex = 0;

            AplicarBusqueda();
        }

        // =========================================================
        // CAMBIO DE CRITERIO
        // =========================================================

        private void CriterioCombo_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            LimpiarCaracteresNoPermitidos();

            if (_datosCargados && !_inicializando)
                AplicarBusqueda();
        }

        // Si se cambia de criterio (por ejemplo de correo a ID), se quitan
        // del cuadro los símbolos que ya no son válidos para ese criterio.
        private void LimpiarCaracteresNoPermitidos()
        {
            if (BusquedaText == null)
                return;

            string actual = BusquedaText.Text;
            var limpio = new StringBuilder(actual.Length);

            foreach (char caracter in actual)
            {
                if (CaracterPermitidoBusqueda(caracter))
                    limpio.Append(caracter);
            }

            if (limpio.Length != actual.Length)
            {
                BusquedaText.Text = limpio.ToString();
            }
        }

        // =========================================================
        // CAMBIO DE SERVICIO
        // =========================================================

        private void FiltroServicioCombo_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (_datosCargados && !_inicializando)
                AplicarBusqueda();
        }

        // =========================================================
        // AGREGAR CRITERIOS
        // =========================================================

        private void AgregarCriterios(params string[] criterios)
        {
            foreach (string criterio in criterios)
            {
                CriterioCombo.Items.Add(criterio);
            }
        }

        // =========================================================
        // BOTÓN BUSCAR
        // =========================================================

        private void BuscarBtn_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!_datosCargados)
            {
                MostrarEstado(
                    "Los registros todavía no están disponibles.",
                    true);

                return;
            }

            if (string.IsNullOrWhiteSpace(
                CategoriaCombo.SelectedItem?.ToString()))
            {
                MostrarEstado(
                    "Selecciona una categoría para realizar la búsqueda.",
                    true);

                return;
            }

            if (string.IsNullOrWhiteSpace(
                CriterioCombo.SelectedItem?.ToString()))
            {
                MostrarEstado(
                    "Selecciona un criterio de búsqueda.",
                    true);

                return;
            }

            AplicarBusqueda();
        }

        // =========================================================
        // ACTUALIZAR REGISTROS DESDE SQLITE
        // =========================================================

        private async void ActualizarBtn_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_actualizandoDatos)
                return;

            _actualizandoDatos = true;

            Button botonActualizar = ObtenerBotonActualizar();
            Button botonBuscar = ObtenerBotonBuscar();

            if (botonActualizar != null)
                botonActualizar.IsEnabled = false;

            if (botonBuscar != null)
                botonBuscar.IsEnabled = false;

            try
            {
                MostrarEstado("Actualizando registros...", false);

                await CargarDatosAsync();

                _datosCargados = true;

                AplicarBusqueda();

                MostrarEstado(
                    "Los registros se actualizaron correctamente.",
                    false);
            }
            catch (Exception ex)
            {
                _datosCargados = false;

                MostrarEstado(
                    "No se pudieron actualizar los registros: " +
                    ex.Message,
                    true);
            }
            finally
            {
                _actualizandoDatos = false;

                botonActualizar = ObtenerBotonActualizar();
                botonBuscar = ObtenerBotonBuscar();

                if (botonActualizar != null)
                    botonActualizar.IsEnabled = true;

                if (botonBuscar != null)
                    botonBuscar.IsEnabled = true;
            }
        }

        // =========================================================
        // APLICAR FILTROS Y BÚSQUEDAS
        // =========================================================

        private void AplicarBusqueda()
        {
            TextBox campoBusqueda = ObtenerCampoBusqueda();

            if (!_datosCargados ||
                CategoriaCombo == null ||
                CriterioCombo == null ||
                campoBusqueda == null ||
                FiltroServicioCombo == null ||
                ClientesListView == null ||
                MascotasListView == null ||
                CitasListView == null ||
                TxtEstadoBusqueda == null)
            {
                return;
            }

            string categoria =
                CategoriaCombo.SelectedItem?.ToString() ?? "";

            string criterio =
                CriterioCombo.SelectedItem?.ToString() ?? "";

            string busqueda =
                campoBusqueda.Text.Trim();

            // CLIENTES

            if (categoria == "Clientes")
            {
                IEnumerable<Cliente> resultados =
                    todosClientes;

                if (!string.IsNullOrWhiteSpace(busqueda))
                {
                    resultados = criterio switch
                    {
                        "Por ID" => resultados.Where(c =>
                            Contiene(c.IDCliente, busqueda)),

                        "Por nombre" => resultados.Where(c =>
                            Contiene(c.Nombre, busqueda)),

                        "Por teléfono" => resultados.Where(c =>
                            Contiene(c.Telefono, busqueda)),

                        "Por correo" => resultados.Where(c =>
                            Contiene(c.Correo, busqueda)),

                        _ => resultados
                    };
                }

                List<Cliente> lista = resultados.ToList();

                ClientesListView.ItemsSource = lista;

                MostrarCantidad(lista.Count, "clientes");
            }

            // MASCOTAS

            else if (categoria == "Mascotas")
            {
                IEnumerable<Mascota> resultados =
                    todasMascotas;

                if (!string.IsNullOrWhiteSpace(busqueda))
                {
                    resultados = criterio switch
                    {
                        "Por ID" => resultados.Where(m =>
                            Contiene(m.IDMascota, busqueda)),

                        "Por nombre" => resultados.Where(m =>
                            Contiene(m.Nombre, busqueda)),

                        "Por especie" => resultados.Where(m =>
                            Contiene(m.Especie, busqueda)),

                        "Por raza" => resultados.Where(m =>
                            Contiene(m.Raza, busqueda)),

                        "Por propietario" => resultados.Where(m =>
                            Contiene(m.IDCliente, busqueda) ||
                            Contiene(m.NombrePropietario, busqueda)),

                        _ => resultados
                    };
                }

                List<Mascota> lista = resultados.ToList();

                MascotasListView.ItemsSource = lista;

                MostrarCantidad(lista.Count, "mascotas");
            }

            // CITAS

            else if (categoria == "Citas")
            {
                IEnumerable<Cita> resultados =
                    todasCitas;

                if (!string.IsNullOrWhiteSpace(busqueda))
                {
                    resultados = criterio switch
                    {
                        "Por ID de cita" => resultados.Where(c =>
                            Contiene(c.IDCita, busqueda)),

                        "Por fecha" => resultados.Where(c =>
                            Contiene(c.Fecha, busqueda)),

                        "Por tipo de servicio" => resultados.Where(c =>
                            Contiene(c.Servicio, busqueda)),

                        "Por mascota" => resultados.Where(c =>
                            Contiene(c.IDMascota, busqueda) ||
                            Contiene(c.NombreMascota, busqueda)),

                        "Por propietario" => resultados.Where(c =>
                            Contiene(c.NombrePropietario, busqueda)),

                        _ => resultados
                    };
                }

                string servicio =
                    FiltroServicioCombo.SelectedItem?.ToString()
                    ?? "Todos";

                if (!string.Equals(
                    servicio,
                    "Todos",
                    StringComparison.OrdinalIgnoreCase))
                {
                    resultados = resultados.Where(c =>
                        string.Equals(
                            NormalizarTexto(c.Servicio),
                            NormalizarTexto(servicio),
                            StringComparison.OrdinalIgnoreCase));
                }

                List<Cita> lista = resultados.ToList();

                CitasListView.ItemsSource = lista;

                MostrarCantidad(lista.Count, "citas");
            }
        }

        // =========================================================
        // COMPARACIÓN DE TEXTO
        // =========================================================

        private static bool Contiene(
            string texto,
            string busqueda)
        {
            if (string.IsNullOrWhiteSpace(texto) ||
                string.IsNullOrWhiteSpace(busqueda))
            {
                return false;
            }

            return NormalizarTexto(texto).Contains(
                NormalizarTexto(busqueda),
                StringComparison.OrdinalIgnoreCase);
        }

        // =========================================================
        // NORMALIZAR TEXTO Y QUITAR ACENTOS PARA COMPARAR
        // =========================================================

        private static string NormalizarTexto(string texto)
        {
            if (string.IsNullOrEmpty(texto))
                return "";

            string normalizado =
                texto.Normalize(NormalizationForm.FormD);

            StringBuilder resultado = new();

            foreach (char caracter in normalizado)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(caracter) !=
                    UnicodeCategory.NonSpacingMark)
                {
                    resultado.Append(caracter);
                }
            }

            return resultado
                .ToString()
                .Normalize(NormalizationForm.FormC)
                .Trim();
        }

        // =========================================================
        // MOSTRAR CANTIDAD DE RESULTADOS
        // =========================================================

        private void MostrarCantidad(
            int cantidad,
            string categoria)
        {
            if (cantidad == 0)
            {
                MostrarEstado(
                    "No se encontraron registros con los filtros seleccionados.",
                    true);
            }
            else if (cantidad == 1)
            {
                MostrarEstado(
                    $"Se encontró 1 registro de {categoria}.",
                    false);
            }
            else
            {
                MostrarEstado(
                    $"Se encontraron {cantidad} registros de {categoria}.",
                    false);
            }
        }

        // =========================================================
        // MOSTRAR MENSAJES DE ESTADO
        // =========================================================

        private void MostrarEstado(
            string mensaje,
            bool esError)
        {
            if (TxtEstadoBusqueda == null)
                return;

            TxtEstadoBusqueda.Text = mensaje;

            TxtEstadoBusqueda.Foreground =
                new SolidColorBrush(
                    esError
                        ? Color.FromArgb(255, 192, 57, 43)
                        : Color.FromArgb(255, 148, 96, 61));
        }

        // =========================================================
        // REGRESAR AL MENÚ
        // =========================================================

        private void RegresarMenuBtn_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
            else
            {
                Frame.Navigate(typeof(MenuPage));
            }
        }
    }
}
