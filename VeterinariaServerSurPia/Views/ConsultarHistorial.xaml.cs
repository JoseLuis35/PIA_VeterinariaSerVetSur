
using Microsoft.Data.Sqlite;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Windows.UI;

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

        public string TextoMostrar =>
            $"{IDMascota} - {Nombre}";
    }

    public sealed class ElementoHistorial
    {
        public string Titulo { get; set; } = "";
        public string Detalle { get; set; } = "";
    }

    public sealed partial class ConsultarHistorial : Page
    {
        private readonly List<MascotaHistorial> mascotas = new();

        private bool cargandoMascotas = false;
        private bool cargandoExpediente = false;

        public ConsultarHistorial()
        {
            InitializeComponent();

            Loaded += ConsultarHistorial_Loaded;
        }

        private async void ConsultarHistorial_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            Loaded -= ConsultarHistorial_Loaded;

            try
            {
                TxtEstado.Text = "Cargando mascotas...";

                await CargarMascotasAsync();
            }
            catch (Exception ex)
            {
                MostrarEstado(
                    $"No se pudieron cargar las mascotas: {ex.Message}",
                    true);

                LimpiarVista();
            }
        }

        private static string LeerTexto(
            SqliteDataReader reader,
            int indice)
        {
            if (reader.IsDBNull(indice))
                return "";

            return Convert.ToString(
                reader.GetValue(indice),
                CultureInfo.InvariantCulture)?.Trim() ?? "";
        }

        private async Task CargarMascotasAsync()
        {
            if (cargandoMascotas)
                return;

            cargandoMascotas = true;

            MascotaCombo.IsEnabled = false;

            try
            {
                mascotas.Clear();

                using SqliteConnection conexion =
                    await App.Database.OpenConnectionAsync();

                using SqliteCommand comando =
                    conexion.CreateCommand();

                comando.CommandText = @"
                    SELECT
                        m.IdMascota,
                        m.Nombre,
                        m.Especie,
                        m.Raza,
                        m.Peso,
                        m.IdCliente,
                        c.Nombre AS Propietario
                    FROM Mascotas m
                    LEFT JOIN Clientes c
                        ON c.IdCliente = m.IdCliente
                    ORDER BY m.Nombre;";

                using SqliteDataReader reader =
                    await comando.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    string idMascota = LeerTexto(reader, 0);
                    string nombre = LeerTexto(reader, 1);

                    if (string.IsNullOrWhiteSpace(idMascota) ||
                        string.IsNullOrWhiteSpace(nombre))
                    {
                        continue;
                    }

                    string peso = "";

                    if (!reader.IsDBNull(4))
                    {
                        peso = Convert.ToString(
                            reader.GetValue(4),
                            CultureInfo.InvariantCulture) ?? "";
                    }

                    mascotas.Add(new MascotaHistorial
                    {
                        IDMascota = idMascota,
                        Nombre = nombre,
                        Especie = LeerTexto(reader, 2),
                        Raza = LeerTexto(reader, 3),
                        Peso = peso,
                        IDCliente = LeerTexto(reader, 5),
                        Propietario = string.IsNullOrWhiteSpace(
                            LeerTexto(reader, 6))
                                ? "Propietario no encontrado"
                                : LeerTexto(reader, 6)
                    });
                }

                MascotaCombo.SelectedItem = null;
                MascotaCombo.ItemsSource = null;
                MascotaCombo.ItemsSource = mascotas;

                LimpiarVista();

                if (mascotas.Count == 0)
                {
                    MostrarEstado(
                        "No hay mascotas registradas. Registra una mascota primero.",
                        true);
                }
                else
                {
                    MostrarEstado(
                        $"Mascotas disponibles: {mascotas.Count}. Selecciona una para consultar su expediente.",
                        false);
                }
            }
            finally
            {
                cargandoMascotas = false;
                MascotaCombo.IsEnabled = true;
            }
        }

        private async void MascotaCombo_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (cargandoMascotas || cargandoExpediente)
                return;

            if (MascotaCombo.SelectedItem is not MascotaHistorial mascota)
            {
                LimpiarVista();
                return;
            }

            if (string.IsNullOrWhiteSpace(mascota.IDMascota))
            {
                MostrarEstado(
                    "La mascota seleccionada no tiene un identificador válido.",
                    true);

                LimpiarVista();
                return;
            }

            await CargarExpedienteAsync(mascota);
        }

        private async Task CargarExpedienteAsync(
            MascotaHistorial mascota)
        {
            if (cargandoExpediente)
                return;

            cargandoExpediente = true;
            MascotaCombo.IsEnabled = false;

            try
            {
                MostrarEstado(
                    $"Cargando expediente de {mascota.Nombre}...",
                    false);

                MostrarInformacionMascota(mascota);

                HistorialListView.ItemsSource = null;
                CartillaListView.ItemsSource = null;

                TxtAlergias.Text = "Consultando información...";
                TxtAntecedentes.Text = "Consultando información...";

                await CargarConsultasAsync(mascota.IDMascota);
                await CargarCartillaAsync(mascota.IDMascota);

                MostrarEstado(
                    $"Expediente cargado correctamente para {mascota.Nombre}.",
                    false);
            }
            catch (Exception ex)
            {
                HistorialListView.ItemsSource = null;
                CartillaListView.ItemsSource = null;

                TxtAlergias.Text = "No se pudo consultar la información.";
                TxtAntecedentes.Text = "No se pudo consultar la información.";

                MostrarEstado(
                    $"No se pudo cargar el expediente: {ex.Message}",
                    true);
            }
            finally
            {
                cargandoExpediente = false;
                MascotaCombo.IsEnabled = true;
            }
        }

        private void MostrarInformacionMascota(
            MascotaHistorial mascota)
        {
            TxtIdMascota.Text = mascota.IDMascota;
            TxtNombreMascota.Text = mascota.Nombre;

            TxtEspecie.Text = string.IsNullOrWhiteSpace(mascota.Especie)
                ? "Sin especie registrada"
                : mascota.Especie;

            TxtRaza.Text = string.IsNullOrWhiteSpace(mascota.Raza)
                ? "Sin raza registrada"
                : mascota.Raza;

            TxtPesoActual.Text = string.IsNullOrWhiteSpace(mascota.Peso)
                ? "Sin peso registrado"
                : $"{mascota.Peso} kg";

            TxtPropietario.Text = mascota.Propietario;
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

        private static DateTime ObtenerFecha(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return DateTime.MinValue;

            string[] formatos =
            {
                "yyyy-MM-dd",
                "yyyy-M-d",
                "dd/MM/yyyy",
                "d/M/yyyy",
                "dd-MM-yyyy",
                "d-M-yyyy"
            };

            if (DateTime.TryParseExact(
                texto.Trim(),
                formatos,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime fecha))
            {
                return fecha.Date;
            }

            if (DateTime.TryParse(
                texto.Trim(),
                new CultureInfo("es-MX"),
                DateTimeStyles.None,
                out fecha))
            {
                return fecha.Date;
            }

            return DateTime.MinValue;
        }

        private static string FormatearFecha(string texto)
        {
            DateTime fecha = ObtenerFecha(texto);

            return fecha == DateTime.MinValue
                ? (string.IsNullOrWhiteSpace(texto)
                    ? "Sin fecha"
                    : texto)
                : fecha.ToString("dd/MM/yyyy");
        }

        private async Task CargarConsultasAsync(string idMascota)
        {
            if (string.IsNullOrWhiteSpace(idMascota))
                throw new ArgumentException(
                    "El identificador de la mascota no es válido.");

            List<ElementoHistorial> registros = new();

            string ultimaAlergia = "Sin información registrada.";
            string ultimoAntecedente = "Sin información registrada.";

            using SqliteConnection conexion =
                await App.Database.OpenConnectionAsync();

            using SqliteCommand comando =
                conexion.CreateCommand();

            comando.CommandText = @"
                SELECT
                    Fecha,
                    Peso,
                    Motivo,
                    Sintomas,
                    Diagnostico,
                    Tratamiento,
                    Alergias,
                    Antecedentes,
                    Observaciones
                FROM HistorialMedico
                WHERE IdMascota = $id
                ORDER BY Fecha DESC, rowid DESC;";

            comando.Parameters.AddWithValue("$id", idMascota);

            using SqliteDataReader reader =
                await comando.ExecuteReaderAsync();

            bool primerRegistro = true;

            while (await reader.ReadAsync())
            {
                string fecha = LeerTexto(reader, 0);
                string peso = LeerTexto(reader, 1);
                string motivo = LeerTexto(reader, 2);
                string sintomas = LeerTexto(reader, 3);
                string diagnostico = LeerTexto(reader, 4);
                string tratamiento = LeerTexto(reader, 5);
                string alergias = LeerTexto(reader, 6);
                string antecedentes = LeerTexto(reader, 7);
                string observaciones = LeerTexto(reader, 8);

                string fechaMostrar = FormatearFecha(fecha);

                string pesoMostrar =
                    string.IsNullOrWhiteSpace(peso)
                        ? "Sin peso registrado"
                        : $"{peso} kg";

                registros.Add(new ElementoHistorial
                {
                    Titulo = $"Consulta médica · {fechaMostrar}",

                    Detalle =
                        $"Peso: {pesoMostrar}\n" +
                        $"Motivo: {ValorOAlternativa(motivo)}\n" +
                        $"Síntomas: {ValorOAlternativa(sintomas)}\n" +
                        $"Diagnóstico: {ValorOAlternativa(diagnostico)}\n" +
                        $"Tratamiento: {ValorOAlternativa(tratamiento)}\n" +
                        $"Alergias: {ValorOAlternativa(alergias)}\n" +
                        $"Antecedentes: {ValorOAlternativa(antecedentes)}\n" +
                        $"Observaciones: {ValorOAlternativa(observaciones)}"
                });

                if (primerRegistro)
                {
                    ultimaAlergia = string.IsNullOrWhiteSpace(alergias)
                        ? "Sin información registrada."
                        : alergias;

                    ultimoAntecedente =
                        string.IsNullOrWhiteSpace(antecedentes)
                            ? "Sin información registrada."
                            : antecedentes;

                    primerRegistro = false;
                }
            }

            HistorialListView.ItemsSource = registros;

            TxtAlergias.Text = ultimaAlergia;
            TxtAntecedentes.Text = ultimoAntecedente;

            if (registros.Count == 0)
            {
                TxtAlergias.Text = "Sin información registrada.";
                TxtAntecedentes.Text = "Sin información registrada.";
            }
        }

        private async Task CargarCartillaAsync(string idMascota)
        {
            if (string.IsNullOrWhiteSpace(idMascota))
                throw new ArgumentException(
                    "El identificador de la mascota no es válido.");

            List<ElementoHistorial> registros = new();

            using SqliteConnection conexion =
                await App.Database.OpenConnectionAsync();

            using SqliteCommand comando =
                conexion.CreateCommand();

            comando.CommandText = @"
                SELECT
                    Tipo,
                    Producto,
                    FechaAplicacion,
                    ProximaFecha,
                    Observaciones
                FROM CartillaMedica
                WHERE IdMascota = $id
                ORDER BY FechaAplicacion DESC, rowid DESC;";

            comando.Parameters.AddWithValue("$id", idMascota);

            using SqliteDataReader reader =
                await comando.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                string tipo = LeerTexto(reader, 0);
                string producto = LeerTexto(reader, 1);
                string aplicacion = LeerTexto(reader, 2);
                string proxima = LeerTexto(reader, 3);
                string observaciones = LeerTexto(reader, 4);

                DateTime fechaProxima = ObtenerFecha(proxima);

                string estado;

                if (fechaProxima == DateTime.MinValue)
                {
                    estado = "Próxima fecha no especificada";
                }
                else if (fechaProxima.Date < DateTime.Today)
                {
                    estado = "Fecha programada vencida";
                }
                else if (fechaProxima.Date == DateTime.Today)
                {
                    estado = "Programada para hoy";
                }
                else
                {
                    estado = "Próxima fecha pendiente";
                }

                string tituloTipo =
                    string.IsNullOrWhiteSpace(tipo)
                        ? "Registro médico"
                        : tipo;

                string tituloProducto =
                    string.IsNullOrWhiteSpace(producto)
                        ? "Producto no especificado"
                        : producto;

                registros.Add(new ElementoHistorial
                {
                    Titulo = $"{tituloTipo} · {tituloProducto}",

                    Detalle =
                        $"Fecha de aplicación: {FormatearFecha(aplicacion)}\n" +
                        $"Próxima fecha: {FormatearFecha(proxima)}\n" +
                        $"Estado: {estado}\n" +
                        $"Observaciones: {ValorOAlternativa(observaciones)}"
                });
            }

            CartillaListView.ItemsSource = registros;
        }

        private static string ValorOAlternativa(string valor)
        {
            return string.IsNullOrWhiteSpace(valor)
                ? "Sin información registrada."
                : valor.Trim();
        }

        private void MostrarEstado(
            string mensaje,
            bool esError)
        {
            TxtEstado.Text = mensaje;

            TxtEstado.Foreground = new SolidColorBrush(
                esError
                    ? Color.FromArgb(255, 192, 57, 43)
                    : Color.FromArgb(255, 128, 105, 79));
        }
    }
}
