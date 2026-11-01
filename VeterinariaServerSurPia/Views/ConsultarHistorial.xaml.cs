
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace VeterinariaServerSurPia.Views
{
    public sealed class MascotaHistorial
    {
        public string IDMascota { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string Especie { get; set; } = "";
        public string Raza { get; set; } = "";
        public string Peso { get; set; } = "";
        public string IDCliente { get; set; } = "";
        public string Propietario { get; set; } = "";

        public string TextoMostrar => $"{IDMascota} - {Nombre}";
    }

    public sealed class ElementoHistorial
    {
        public string Titulo { get; set; } = "";
        public string Detalle { get; set; } = "";
    }

    public sealed partial class ConsultarHistorial : Page
    {
        private readonly List<MascotaHistorial> mascotas = new();

        private string Ruta(string archivo)
        {
            return Path.Combine(AppContext.BaseDirectory, archivo);
        }

        public ConsultarHistorial()
        {
            InitializeComponent();
            CargarMascotas();
        }

        private void CargarMascotas()
        {
            string rutaClientes = Ruta("clientes.txt");
            string rutaMascotas = Ruta("mascotas.txt");

            var clientes = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

            if (File.Exists(rutaClientes))
            {
                foreach (string linea in File.ReadAllLines(rutaClientes))
                {
                    string[] p = linea.Split(',');

                    if (p.Length >= 2 && !string.IsNullOrWhiteSpace(p[0]))
                    {
                        clientes[p[0].Trim()] = p[1].Trim();
                    }
                }
            }

            if (File.Exists(rutaMascotas))
            {
                foreach (string linea in File.ReadAllLines(rutaMascotas))
                {
                    string[] p = linea.Split(',');

                    if (p.Length < 8)
                        continue;

                    string idCliente = p[7].Trim();

                    mascotas.Add(new MascotaHistorial
                    {
                        IDMascota = p[0].Trim(),
                        Nombre = p[1].Trim(),
                        Especie = p[2].Trim(),
                        Raza = p[3].Trim(),
                        Peso = p[6].Trim(),
                        IDCliente = idCliente,
                        Propietario = clientes.TryGetValue(
                            idCliente, out string? nombre)
                                ? nombre
                                : "Propietario no encontrado"
                    });
                }
            }

            MascotaCombo.ItemsSource = mascotas;

            if (mascotas.Count == 0)
            {
                TxtEstado.Text =
                    "No hay mascotas registradas. Registra una mascota primero.";
            }
        }

        private void MascotaCombo_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (MascotaCombo.SelectedItem is not MascotaHistorial mascota)
            {
                LimpiarVista();
                return;
            }

            TxtIdMascota.Text = mascota.IDMascota;
            TxtNombreMascota.Text = mascota.Nombre;
            TxtEspecie.Text = mascota.Especie;
            TxtRaza.Text = mascota.Raza;
            TxtPesoActual.Text = string.IsNullOrWhiteSpace(mascota.Peso)
                ? "Sin peso registrado"
                : $"{mascota.Peso} kg";
            TxtPropietario.Text = mascota.Propietario;

            CargarConsultas(mascota.IDMascota);
            CargarCartilla(mascota.IDMascota);

            TxtEstado.Text =
                $"Expediente cargado para {mascota.Nombre}.";
        }

        private void LimpiarVista()
        {
            TxtIdMascota.Text = "—";
            TxtNombreMascota.Text = "—";
            TxtEspecie.Text = "—";
            TxtRaza.Text = "—";
            TxtPesoActual.Text = "—";
            TxtPropietario.Text = "—";
            TxtAlergias.Text = "Sin información registrada.";
            TxtAntecedentes.Text = "Sin información registrada.";
            HistorialListView.ItemsSource = null;
            CartillaListView.ItemsSource = null;
        }

        private void CargarConsultas(string idMascota)
        {
            string ruta = Ruta("historial_medico.txt");
            var registros = new List<string[]>();

            if (File.Exists(ruta))
            {
                foreach (string linea in File.ReadAllLines(ruta))
                {
                    string[] p = linea.Split('|');

                    if (p.Length >= 11 &&
                        p[1].Equals(
                            idMascota,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        registros.Add(p);
                    }
                }
            }

            var ordenados = registros
                .OrderByDescending(p => ObtenerFecha(p[2]))
                .ToList();

            HistorialListView.ItemsSource = ordenados
                .Select(p => new ElementoHistorial
                {
                    Titulo = $"Consulta médica · {p[2]}",
                    Detalle =
                        $"Peso: {p[3]} kg\n" +
                        $"Motivo: {p[4]}\n" +
                        $"Síntomas: {p[5]}\n" +
                        $"Diagnóstico: {p[6]}\n" +
                        $"Tratamiento: {p[7]}\n" +
                        $"Alergias: {p[8]}\n" +
                        $"Antecedentes: {p[9]}\n" +
                        $"Observaciones: {p[10]}"
                })
                .ToList();

            string[]? ultimo = ordenados.FirstOrDefault();

            if (ultimo != null)
            {
                TxtAlergias.Text = ultimo[8];
                TxtAntecedentes.Text = ultimo[9];
            }
            else
            {
                TxtAlergias.Text = "Sin información registrada.";
                TxtAntecedentes.Text = "Sin información registrada.";
            }
        }

        private DateTime ObtenerFecha(string texto)
        {
            return DateTime.TryParseExact(
                texto,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime fecha)
                    ? fecha
                    : DateTime.MinValue;
        }

        private void CargarCartilla(string idMascota)
        {
            string ruta = Ruta("cartilla_medica.txt");
            var registros = new List<string[]>();

            if (File.Exists(ruta))
            {
                foreach (string linea in File.ReadAllLines(ruta))
                {
                    string[] p = linea.Split('|');

                    if (p.Length >= 7 &&
                        p[1].Equals(
                            idMascota,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        registros.Add(p);
                    }
                }
            }

            CartillaListView.ItemsSource = registros
                .OrderByDescending(p => ObtenerFecha(p[4]))
                .Select(p =>
                {
                    DateTime proxima = ObtenerFecha(p[5]);
                    string estado;

                    if (proxima == DateTime.MinValue)
                    {
                        estado = "Próxima fecha no especificada";
                    }
                    else if (proxima.Date < DateTime.Today)
                    {
                        estado = "Fecha programada vencida";
                    }
                    else if (proxima.Date == DateTime.Today)
                    {
                        estado = "Programada para hoy";
                    }
                    else
                    {
                        estado = "Próxima fecha pendiente";
                    }

                    return new ElementoHistorial
                    {
                        Titulo = $"{p[2]} · {p[3]}",
                        Detalle =
                            $"Fecha de aplicación: {p[4]}\n" +
                            $"Próxima fecha: {p[5]}\n" +
                            $"Estado: {estado}\n" +
                            $"Observaciones: {p[6]}"
                    };
                })
                .ToList();
        }
    }
}
