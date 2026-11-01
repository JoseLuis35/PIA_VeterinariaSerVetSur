
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

        public BusquedaConsulta()
        {
            InitializeComponent();

            CategoriaCombo.SelectedIndex = 0;
            FiltroServicioCombo.SelectedIndex = 0;

            CargarDatos();
            AplicarBusqueda();
        }

        private string ObtenerRuta(string archivo)
        {
            return Path.Combine(
                AppContext.BaseDirectory,
                archivo);
        }

        private void CargarDatos()
        {
            todosClientes.Clear();
            todasMascotas.Clear();
            todasCitas.Clear();

            LeerArchivo("clientes.txt", 4, p =>
            {
                if (string.IsNullOrWhiteSpace(p[0]))
                    return;

                todosClientes.Add(new Cliente
                {
                    IDCliente = p[0].Trim(),
                    Nombre = p[1].Trim(),
                    Telefono = p[2].Trim(),
                    Correo = p[3].Trim()
                });
            });

            LeerArchivo("mascotas.txt", 8, p =>
            {
                if (string.IsNullOrWhiteSpace(p[0]))
                    return;

                todasMascotas.Add(new Mascota
                {
                    IDMascota = p[0].Trim(),
                    Nombre = p[1].Trim(),
                    Especie = p[2].Trim(),
                    Raza = p[3].Trim(),
                    Edad = p[4].Trim(),
                    Sexo = p[5].Trim(),
                    Peso = p[6].Trim(),
                    IDCliente = p[7].Trim()
                });
            });

            LeerArchivo("citas.txt", 5, p =>
            {
                if (string.IsNullOrWhiteSpace(p[0]))
                    return;

                todasCitas.Add(new Cita
                {
                    IDCita = p[0].Trim(),
                    IDMascota = p[1].Trim(),
                    Fecha = p[2].Trim(),
                    Hora = p[3].Trim(),
                    Servicio = p[4].Trim(),
                    Observaciones = p.Length > 5
                        ? string.Join(",", p.Skip(5)).Trim()
                        : ""
                });
            });

            // Relacionar mascotas con clientes.
            foreach (Mascota mascota in todasMascotas)
            {
                Cliente? propietario = todosClientes.FirstOrDefault(
                    c => c.IDCliente.Equals(
                        mascota.IDCliente,
                        StringComparison.OrdinalIgnoreCase));

                mascota.NombrePropietario =
                    propietario?.Nombre ?? "No encontrado";
            }

            // Relacionar citas con mascotas y propietarios.
            foreach (Cita cita in todasCitas)
            {
                Mascota? mascota = todasMascotas.FirstOrDefault(
                    m => m.IDMascota.Equals(
                        cita.IDMascota,
                        StringComparison.OrdinalIgnoreCase));

                if (mascota == null)
                {
                    cita.NombreMascota = "No encontrada";
                    cita.NombrePropietario = "No encontrado";
                    continue;
                }

                cita.NombreMascota = mascota.Nombre;
                cita.NombrePropietario = mascota.NombrePropietario;
            }
        }

        private void LeerArchivo(
            string archivo,
            int minimoCampos,
            Action<string[]> agregar)
        {
            string ruta = ObtenerRuta(archivo);

            if (!File.Exists(ruta))
                return;

            foreach (string linea in File.ReadLines(ruta))
            {
                if (string.IsNullOrWhiteSpace(linea))
                    continue;

                string[] partes = linea.Split(',');

                if (partes.Length >= minimoCampos)
                {
                    agregar(partes);
                }
            }
        }

        private void CategoriaCombo_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (CriterioCombo == null ||
                BusquedaText == null ||
                FiltroServicioCombo == null ||
                ClientesListView == null ||
                MascotasListView == null ||
                CitasListView == null)
            {
                return;
            }

            CriterioCombo.Items.Clear();
            BusquedaText.Text = "";

            FiltroServicioCombo.Visibility = Visibility.Collapsed;

            ClientesListView.Visibility = Visibility.Collapsed;
            MascotasListView.Visibility = Visibility.Collapsed;
            CitasListView.Visibility = Visibility.Collapsed;

            string categoria =
                CategoriaCombo.SelectedItem?.ToString() ?? "";

            if (categoria == "Clientes")
            {
                AgregarCriterios(
                    "Por ID",
                    "Por nombre",
                    "Por teléfono",
                    "Por correo");

                ClientesListView.Visibility = Visibility.Visible;
            }
            else if (categoria == "Mascotas")
            {
                AgregarCriterios(
                    "Por ID",
                    "Por nombre",
                    "Por especie",
                    "Por raza",
                    "Por propietario");

                MascotasListView.Visibility = Visibility.Visible;
            }
            else if (categoria == "Citas")
            {
                AgregarCriterios(
                    "Por ID de cita",
                    "Por fecha",
                    "Por tipo de servicio",
                    "Por mascota",
                    "Por propietario");

                FiltroServicioCombo.Visibility = Visibility.Visible;
                FiltroServicioCombo.SelectedIndex = 0;
                CitasListView.Visibility = Visibility.Visible;
            }

            if (CriterioCombo.Items.Count > 0)
                CriterioCombo.SelectedIndex = 0;

            AplicarBusqueda();
        }

        private void AgregarCriterios(params string[] criterios)
        {
            foreach (string criterio in criterios)
            {
                CriterioCombo.Items.Add(criterio);
            }
        }

        private void BuscarBtn_Click(
            object sender,
            RoutedEventArgs e)
        {
            AplicarBusqueda();
        }

        private void ActualizarBtn_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                CargarDatos();
                AplicarBusqueda();
            }
            catch (Exception ex)
            {
                MostrarEstado(
                    $"No se pudieron actualizar los registros: {ex.Message}",
                    true);
            }
        }

        private void AplicarBusqueda()
        {
            if (CategoriaCombo == null ||
                CriterioCombo == null ||
                BusquedaText == null ||
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

            string busqueda = BusquedaText.Text.Trim();

            if (categoria == "Clientes")
            {
                IEnumerable<Cliente> resultados = todosClientes;

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
            else if (categoria == "Mascotas")
            {
                IEnumerable<Mascota> resultados = todasMascotas;

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
            else if (categoria == "Citas")
            {
                IEnumerable<Cita> resultados = todasCitas;

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
                    FiltroServicioCombo.SelectedItem?.ToString() ?? "Todos";

                if (servicio != "Todos")
                {
                    resultados = resultados.Where(c =>
                        c.Servicio.Equals(
                            servicio,
                            StringComparison.OrdinalIgnoreCase));
                }

                List<Cita> lista = resultados.ToList();
                CitasListView.ItemsSource = lista;

                MostrarCantidad(lista.Count, "citas");
            }
        }

        private static bool Contiene(
            string texto,
            string busqueda)
        {
            return texto.Contains(
                busqueda,
                StringComparison.OrdinalIgnoreCase);
        }

        private void MostrarCantidad(int cantidad, string categoria)
        {
            if (cantidad == 0)
            {
                MostrarEstado(
                    "No se encontraron registros con los filtros seleccionados.",
                    true);
            }
            else
            {
                MostrarEstado(
                    $"Se encontraron {cantidad} registros de {categoria}.",
                    false);
            }
        }

        private void MostrarEstado(string mensaje, bool esError)
        {
            if (TxtEstadoBusqueda == null)
                return;

            TxtEstadoBusqueda.Text = mensaje;
            TxtEstadoBusqueda.Foreground =
                new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    esError
                        ? Microsoft.UI.Colors.OrangeRed
                        : Microsoft.UI.Colors.LightGreen);
        }

        private void RegresarMenuBtn_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
                Frame.GoBack();
            else
                Frame.Navigate(typeof(MenuPage));
        }
    }
}
