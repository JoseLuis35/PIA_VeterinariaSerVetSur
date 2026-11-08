
using Microsoft.Data.Sqlite;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
    public sealed class MascotaExpediente
    {
        public string IDMascota { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string Especie { get; set; } = "";
        public string Raza { get; set; } = "";
        public string Peso { get; set; } = "";
        public string IDCliente { get; set; } = "";
        public string NombrePropietario { get; set; } = "";

        public string TextoMostrar =>
            $"{IDMascota} - {Nombre} ({Especie})";
    }

    public sealed class RegistroExpedienteVista
    {
        public string Titulo { get; set; } = "";
        public string Detalle { get; set; } = "";
    }

    public sealed partial class HistorialMedico : Page
    {
        private readonly List<MascotaExpediente> mascotas = new();

        private bool _ajustandoTexto;
        private bool _guardandoConsulta;
        private bool _guardandoCartilla;
        private bool _cargandoMascotas;

        private static readonly CultureInfo CulturaInvariante =
            CultureInfo.InvariantCulture;

        private static readonly Color ColorError =
            Color.FromArgb(255, 173, 57, 43);

        private static readonly Color ColorExito =
            Color.FromArgb(255, 46, 125, 50);

        public HistorialMedico()
        {
            InitializeComponent();
            Loaded += HistorialMedico_Loaded;
        }

        // ==========================================
        // CARGA INICIAL
        // ==========================================

        private async void HistorialMedico_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            Loaded -= HistorialMedico_Loaded;

            try
            {
                TxtMensaje.Text = "Cargando mascotas...";

                await CargarMascotasAsync();

                if (mascotas.Count > 0 &&
                    MascotaCombo.SelectedItem == null)
                {
                    TxtMensaje.Text =
                        "Selecciona una mascota para comenzar.";
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje(
                    $"No se pudieron cargar las mascotas: {ex.Message}",
                    true);
            }
        }

        // ==========================================
        // LEER TEXTO DE SQLITE DE FORMA SEGURA
        // ==========================================

        private static string LeerTexto(
            SqliteDataReader reader,
            int indice)
        {
            if (reader.IsDBNull(indice))
                return "";

            return Convert.ToString(
                reader.GetValue(indice),
                CulturaInvariante) ?? "";
        }

        // ==========================================
        // CARGAR MASCOTAS DESDE SQLITE
        // ==========================================

        private async Task CargarMascotasAsync()
        {
            if (_cargandoMascotas)
                return;

            _cargandoMascotas = true;

            string? idAnterior =
                (MascotaCombo.SelectedItem as MascotaExpediente)?.IDMascota;

            try
            {
                List<MascotaExpediente> mascotasCargadas = new();

                using SqliteConnection conexion =
                    await App.Database.OpenConnectionAsync();

                using SqliteCommand comando = conexion.CreateCommand();

                comando.CommandText = @"
                    SELECT
                        m.IdMascota,
                        m.Nombre,
                        m.Especie,
                        m.Raza,
                        m.Peso,
                        m.IdCliente,
                        c.Nombre
                    FROM Mascotas m
                    LEFT JOIN Clientes c
                        ON c.IdCliente = m.IdCliente
                    ORDER BY m.Nombre COLLATE NOCASE;";

                using SqliteDataReader reader =
                    await comando.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    string peso = "";

                    if (!reader.IsDBNull(4))
                    {
                        if (decimal.TryParse(
                            LeerTexto(reader, 4),
                            NumberStyles.Number,
                            CulturaInvariante,
                            out decimal pesoLeido))
                        {
                            peso = pesoLeido.ToString(
                                "0.##",
                                CulturaInvariante);
                        }
                    }

                    string nombrePropietario =
                        LeerTexto(reader, 6).Trim();

                    mascotasCargadas.Add(new MascotaExpediente
                    {
                        IDMascota = LeerTexto(reader, 0).Trim(),
                        Nombre = LeerTexto(reader, 1).Trim(),
                        Especie = LeerTexto(reader, 2).Trim(),
                        Raza = LeerTexto(reader, 3).Trim(),
                        Peso = peso,
                        IDCliente = LeerTexto(reader, 5).Trim(),

                        NombrePropietario =
                            string.IsNullOrWhiteSpace(nombrePropietario)
                                ? "Propietario no encontrado"
                                : nombrePropietario
                    });
                }

                mascotas.Clear();
                mascotas.AddRange(mascotasCargadas);

                MascotaCombo.ItemsSource = null;
                MascotaCombo.ItemsSource = mascotas;

                MascotaExpediente? anterior = mascotas.FirstOrDefault(m =>
                    string.Equals(
                        m.IDMascota,
                        idAnterior,
                        StringComparison.OrdinalIgnoreCase));

                if (anterior != null)
                {
                    MascotaCombo.SelectedItem = anterior;
                }
                else
                {
                    MascotaCombo.SelectedItem = null;
                    LimpiarExpediente();
                }

                if (mascotas.Count == 0)
                {
                    MostrarMensaje(
                        "No hay mascotas registradas. Primero registra una mascota.",
                        true);

                    LimpiarExpediente();
                }
            }
            finally
            {
                _cargandoMascotas = false;
            }
        }

        // ==========================================
        // SELECCIONAR MASCOTA
        // ==========================================

        private async void MascotaCombo_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (_cargandoMascotas)
                return;

            if (MascotaCombo.SelectedItem is not MascotaExpediente mascota)
            {
                LimpiarExpediente();
                return;
            }

            TxtIdMascota.Text = mascota.IDMascota;
            TxtNombreMascota.Text = mascota.Nombre;

            TxtEspecieRaza.Text =
                string.IsNullOrWhiteSpace(mascota.Raza)
                    ? mascota.Especie
                    : $"{mascota.Especie} - {mascota.Raza}";

            TxtPropietario.Text = mascota.NombrePropietario;
            TxtPeso.Text = mascota.Peso;

            try
            {
                TxtMensaje.Text = "Cargando expediente médico...";

                await CargarHistorialAsync(mascota.IDMascota);
                await CargarCartillaAsync(mascota.IDMascota);

                TxtMensaje.Text =
                    "Expediente cargado correctamente.";
            }
            catch (Exception ex)
            {
                MostrarMensaje(
                    $"No se pudo cargar el expediente: {ex.Message}",
                    true);
            }
        }

        // ==========================================
        // LIMPIAR EXPEDIENTE
        // ==========================================

        private void LimpiarExpediente()
        {
            TxtIdMascota.Text = "—";
            TxtNombreMascota.Text = "—";
            TxtEspecieRaza.Text = "—";
            TxtPropietario.Text = "—";
            TxtPeso.Text = "";

            HistorialListView.ItemsSource = null;
            CartillaListView.ItemsSource = null;
        }

        // ==========================================
        // CONVERTIR FECHAS
        // ==========================================

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
                "d-M-yyyy",
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-dd HH:mm"
            };

            if (DateTime.TryParseExact(
                texto.Trim(),
                formatos,
                CulturaInvariante,
                DateTimeStyles.None,
                out DateTime fecha))
            {
                return fecha.Date;
            }

            if (DateTime.TryParse(
                texto,
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
                ? texto
                : fecha.ToString("dd/MM/yyyy", CulturaInvariante);
        }

        // ==========================================
        // CARGAR HISTORIAL MÉDICO
        // ==========================================

        private async Task CargarHistorialAsync(string idMascota)
        {
            if (string.IsNullOrWhiteSpace(idMascota))
            {
                HistorialListView.ItemsSource = null;
                return;
            }

            List<RegistroExpedienteVista> vista = new();

            using SqliteConnection conexion =
                await App.Database.OpenConnectionAsync();

            using SqliteCommand comando = conexion.CreateCommand();

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

                vista.Add(new RegistroExpedienteVista
                {
                    Titulo = $"Consulta del {fechaMostrar}",

                    Detalle =
                        $"Peso: {peso} kg\n" +
                        $"Motivo: {motivo}\n" +
                        $"Síntomas: {sintomas}\n" +
                        $"Diagnóstico: {diagnostico}\n" +
                        $"Tratamiento: {tratamiento}\n" +
                        $"Alergias: {alergias}\n" +
                        $"Antecedentes: {antecedentes}\n" +
                        $"Observaciones: {observaciones}"
                });
            }

            HistorialListView.ItemsSource = vista;
        }

        // ==========================================
        // CARGAR CARTILLA MÉDICA
        // ==========================================

        private async Task CargarCartillaAsync(string idMascota)
        {
            if (string.IsNullOrWhiteSpace(idMascota))
            {
                CartillaListView.ItemsSource = null;
                return;
            }

            List<RegistroExpedienteVista> vista = new();

            using SqliteConnection conexion =
                await App.Database.OpenConnectionAsync();

            using SqliteCommand comando = conexion.CreateCommand();

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

                vista.Add(new RegistroExpedienteVista
                {
                    Titulo = $"{tipo}: {producto}",

                    Detalle =
                        $"Aplicación: {FormatearFecha(aplicacion)}\n" +
                        $"Próxima fecha: {FormatearFecha(proxima)}\n" +
                        $"Observaciones: {observaciones}"
                });
            }

            CartillaListView.ItemsSource = vista;
        }

        // ==========================================
        // VALIDAR PESO
        // ==========================================

        private bool ValidarPeso(out decimal peso)
        {
            peso = 0;

            string texto = TxtPeso.Text
                .Trim()
                .Replace(',', '.');

            if (string.IsNullOrWhiteSpace(texto))
            {
                MostrarMensaje(
                    "Escribe el peso actual de la mascota.",
                    true);

                TxtPeso.Focus(FocusState.Programmatic);
                return false;
            }

            if (!decimal.TryParse(
                texto,
                NumberStyles.AllowDecimalPoint,
                CulturaInvariante,
                out peso))
            {
                MostrarMensaje(
                    "Escribe un peso válido. Puedes utilizar hasta dos decimales.",
                    true);

                TxtPeso.Focus(FocusState.Programmatic);
                return false;
            }

            if (peso <= 0 || peso > 1000)
            {
                MostrarMensaje(
                    "El peso debe ser mayor que cero y no superar los 1000 kg.",
                    true);

                TxtPeso.Focus(FocusState.Programmatic);
                return false;
            }

            return true;
        }

        // ==========================================
        // VALIDAR FECHA DE CONSULTA
        // ==========================================

        private bool ValidarFechaConsulta()
        {
            DateTime fecha = FechaConsultaPicker.Date.Date;
            DateTime hoy = DateTime.Today;

            if (fecha > hoy)
            {
                MostrarMensaje(
                    "La fecha de consulta no puede ser posterior al día de hoy.",
                    true);

                return false;
            }

            return true;
        }

        // ==========================================
        // VALIDAR CAMPOS CLÍNICOS
        // ==========================================

        private bool ValidarCamposConsulta(string[] valores)
        {
            if (string.IsNullOrWhiteSpace(valores[0]))
            {
                MostrarMensaje(
                    "Escribe el motivo de la consulta.",
                    true);

                TxtMotivo.Focus(FocusState.Programmatic);
                return false;
            }

            if (string.IsNullOrWhiteSpace(valores[2]))
            {
                MostrarMensaje(
                    "Escribe el diagnóstico de la consulta.",
                    true);

                TxtDiagnostico.Focus(FocusState.Programmatic);
                return false;
            }

            string[] nombres =
            {
                "Motivo",
                "Síntomas",
                "Diagnóstico",
                "Tratamiento",
                "Alergias",
                "Antecedentes",
                "Observaciones"
            };

            for (int i = 0; i < valores.Length; i++)
            {
                if (valores[i].Length > 2000)
                {
                    MostrarMensaje(
                        $"El campo {nombres[i]} admite un máximo de 2000 caracteres.",
                        true);

                    return false;
                }
            }

            return true;
        }

        // ==========================================
        // GUARDAR CONSULTA MÉDICA
        // ==========================================

        private async void BtnGuardarConsulta_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_guardandoConsulta)
                return;

            if (MascotaCombo.SelectedItem is not MascotaExpediente mascota)
            {
                MostrarMensaje(
                    "Primero selecciona una mascota.",
                    true);

                return;
            }

            if (!ValidarPeso(out decimal peso))
                return;

            if (!ValidarFechaConsulta())
                return;

            string[] valores =
            {
                TxtMotivo.Text.Trim(),
                TxtSintomas.Text.Trim(),
                TxtDiagnostico.Text.Trim(),
                TxtTratamiento.Text.Trim(),
                TxtAlergias.Text.Trim(),
                TxtAntecedentes.Text.Trim(),
                TxtObservaciones.Text.Trim()
            };

            if (!ValidarCamposConsulta(valores))
                return;

            _guardandoConsulta = true;

            Button? botonGuardar = sender as Button;

            if (botonGuardar != null)
                botonGuardar.IsEnabled = false;

            try
            {
                string idRegistro = Guid.NewGuid().ToString("N");

                string fecha = FechaConsultaPicker.Date.Date
                    .ToString("yyyy-MM-dd", CulturaInvariante);

                string alergias =
                    string.IsNullOrWhiteSpace(valores[4])
                        ? "No especificadas"
                        : valores[4];

                string antecedentes =
                    string.IsNullOrWhiteSpace(valores[5])
                        ? "No especificados"
                        : valores[5];

                using SqliteConnection conexion =
                    await App.Database.OpenConnectionAsync();

                using SqliteCommand comando = conexion.CreateCommand();

                comando.CommandText = @"
                    INSERT INTO HistorialMedico
                    (
                        IdRegistro,
                        IdMascota,
                        Fecha,
                        Peso,
                        Motivo,
                        Sintomas,
                        Diagnostico,
                        Tratamiento,
                        Alergias,
                        Antecedentes,
                        Observaciones
                    )
                    VALUES
                    (
                        $id,
                        $mascota,
                        $fecha,
                        $peso,
                        $motivo,
                        $sintomas,
                        $diagnostico,
                        $tratamiento,
                        $alergias,
                        $antecedentes,
                        $observaciones
                    );";

                comando.Parameters.AddWithValue("$id", idRegistro);
                comando.Parameters.AddWithValue("$mascota", mascota.IDMascota);
                comando.Parameters.AddWithValue("$fecha", fecha);
                comando.Parameters.AddWithValue("$peso", (double)peso);
                comando.Parameters.AddWithValue("$motivo", valores[0]);
                comando.Parameters.AddWithValue("$sintomas", valores[1]);
                comando.Parameters.AddWithValue("$diagnostico", valores[2]);
                comando.Parameters.AddWithValue("$tratamiento", valores[3]);
                comando.Parameters.AddWithValue("$alergias", alergias);
                comando.Parameters.AddWithValue("$antecedentes", antecedentes);
                comando.Parameters.AddWithValue("$observaciones", valores[6]);

                await comando.ExecuteNonQueryAsync();

                await CargarHistorialAsync(mascota.IDMascota);

                MostrarMensaje(
                    "Consulta médica guardada correctamente.",
                    false);

                TxtMotivo.Text = "";
                TxtSintomas.Text = "";
                TxtDiagnostico.Text = "";
                TxtTratamiento.Text = "";
                TxtObservaciones.Text = "";

                TxtAlergias.Text = alergias;
                TxtAntecedentes.Text = antecedentes;

                TxtPeso.Text = peso.ToString(
                    "0.##",
                    CulturaInvariante);
            }
            catch (SqliteException ex)
            {
                MostrarMensaje(
                    $"Error de base de datos: {ex.Message}",
                    true);
            }
            catch (Exception ex)
            {
                MostrarMensaje(
                    $"No se pudo guardar la consulta: {ex.Message}",
                    true);
            }
            finally
            {
                _guardandoConsulta = false;

                if (botonGuardar != null)
                    botonGuardar.IsEnabled = true;
            }
        }

        // ==========================================
        // VALIDAR CARTILLA MÉDICA
        // ==========================================

        private bool ValidarCartilla(
            string tipo,
            string producto,
            string observaciones,
            DateTime? fechaAplicacionSel,
            DateTime? fechaProximaSel)
        {
            if (tipo != "Vacuna" &&
                tipo != "Desparasitación")
            {
                MostrarMensaje(
                    "Selecciona un tipo de registro válido.",
                    true);

                return false;
            }

            if (string.IsNullOrWhiteSpace(producto))
            {
                MostrarMensaje(
                    "Escribe el nombre de la vacuna o del producto.",
                    true);

                TxtProducto.Focus(FocusState.Programmatic);
                return false;
            }

            if (producto.Length > 200)
            {
                MostrarMensaje(
                    "El nombre del producto admite un máximo de 200 caracteres.",
                    true);

                return false;
            }

            if (observaciones.Length > 2000)
            {
                MostrarMensaje(
                    "Las observaciones admiten un máximo de 2000 caracteres.",
                    true);

                return false;
            }

            if (fechaAplicacionSel == null)
            {
                MostrarMensaje(
                    "Selecciona la fecha de aplicación.",
                    true);

                return false;
            }

            if (fechaProximaSel == null)
            {
                MostrarMensaje(
                    "Selecciona la próxima fecha programada.",
                    true);

                return false;
            }

            DateTime fechaAplicacion = fechaAplicacionSel.Value.Date;
            DateTime fechaProxima = fechaProximaSel.Value.Date;

            if (fechaAplicacion > DateTime.Today)
            {
                MostrarMensaje(
                    "La fecha de aplicación no puede ser futura.",
                    true);

                return false;
            }

            // La próxima fecha no puede ser un día anterior a hoy.
            if (fechaProxima < DateTime.Today)
            {
                MostrarMensaje(
                    "La próxima fecha programada no puede ser una fecha pasada.",
                    true);

                return false;
            }

            if (fechaProxima < fechaAplicacion)
            {
                MostrarMensaje(
                    "La próxima fecha no puede ser anterior a la fecha de aplicación.",
                    true);

                return false;
            }

            return true;
        }

        // ==========================================
        // GUARDAR CARTILLA MÉDICA
        // ==========================================

        private async void BtnGuardarCartilla_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_guardandoCartilla)
                return;

            if (MascotaCombo.SelectedItem is not MascotaExpediente mascota)
            {
                MostrarMensaje(
                    "Primero selecciona una mascota.",
                    true);

                return;
            }

            if (TipoCartillaCombo.SelectedItem is not ComboBoxItem tipoItem)
            {
                MostrarMensaje(
                    "Selecciona el tipo de registro.",
                    true);

                return;
            }

            string tipo =
                tipoItem.Content?.ToString()?.Trim() ?? "";

            string producto = TxtProducto.Text.Trim();

            string observaciones =
                TxtObservacionesCartilla.Text.Trim();

            DateTime? fechaAplicacionSel =
                FechaAplicacionPicker.SelectedDate?.Date;

            DateTime? fechaProximaSel =
                FechaProximaPicker.SelectedDate?.Date;

            if (!ValidarCartilla(
                tipo,
                producto,
                observaciones,
                fechaAplicacionSel,
                fechaProximaSel))
            {
                return;
            }

            DateTime fechaAplicacion = fechaAplicacionSel!.Value;
            DateTime fechaProxima = fechaProximaSel!.Value;

            _guardandoCartilla = true;

            Button? botonGuardar = sender as Button;

            if (botonGuardar != null)
                botonGuardar.IsEnabled = false;

            try
            {
                using SqliteConnection conexion =
                    await App.Database.OpenConnectionAsync();

                using SqliteCommand comando = conexion.CreateCommand();

                comando.CommandText = @"
                    INSERT INTO CartillaMedica
                    (
                        IdRegistro,
                        IdMascota,
                        Tipo,
                        Producto,
                        FechaAplicacion,
                        ProximaFecha,
                        Observaciones
                    )
                    VALUES
                    (
                        $id,
                        $mascota,
                        $tipo,
                        $producto,
                        $aplicacion,
                        $proxima,
                        $observaciones
                    );";

                comando.Parameters.AddWithValue(
                    "$id",
                    Guid.NewGuid().ToString("N"));

                comando.Parameters.AddWithValue(
                    "$mascota",
                    mascota.IDMascota);

                comando.Parameters.AddWithValue(
                    "$tipo",
                    tipo);

                comando.Parameters.AddWithValue(
                    "$producto",
                    producto);

                comando.Parameters.AddWithValue(
                    "$aplicacion",
                    fechaAplicacion.ToString(
                        "yyyy-MM-dd",
                        CulturaInvariante));

                comando.Parameters.AddWithValue(
                    "$proxima",
                    fechaProxima.ToString(
                        "yyyy-MM-dd",
                        CulturaInvariante));

                comando.Parameters.AddWithValue(
                    "$observaciones",
                    observaciones);

                await comando.ExecuteNonQueryAsync();

                await CargarCartillaAsync(mascota.IDMascota);

                MostrarMensaje(
                    "Registro de cartilla guardado correctamente.",
                    false);

                TxtProducto.Text = "";
                TxtObservacionesCartilla.Text = "";
            }
            catch (SqliteException ex)
            {
                MostrarMensaje(
                    $"Error de base de datos: {ex.Message}",
                    true);
            }
            catch (Exception ex)
            {
                MostrarMensaje(
                    $"No se pudo guardar el registro: {ex.Message}",
                    true);
            }
            finally
            {
                _guardandoCartilla = false;

                if (botonGuardar != null)
                    botonGuardar.IsEnabled = true;
            }
        }

        // ==========================================
        // FILTROS DE ESCRITURA
        // ==========================================

        private string FiltrarTexto(string texto)
        {
            texto = Regex.Replace(
                texto,
                @"[^\p{L}\p{N}\s.,:;¿?¡!\-()/_+#%°|]",
                "");

            texto = texto.TrimStart();

            texto = Regex.Replace(
                texto,
                @"[ ]{2,}",
                " ");

            return texto;
        }

        private void ValidarCampo(TextBox sender)
        {
            if (_ajustandoTexto)
                return;

            string textoOriginal = sender.Text;
            int posicionAnterior = sender.SelectionStart;

            string textoValidado =
                FiltrarTexto(textoOriginal);

            if (textoOriginal == textoValidado)
                return;

            string textoAntesCursor =
                textoOriginal.Substring(
                    0,
                    Math.Min(
                        posicionAnterior,
                        textoOriginal.Length));

            string cursorValidado =
                FiltrarTexto(textoAntesCursor);

            _ajustandoTexto = true;

            try
            {
                sender.Text = textoValidado;

                sender.SelectionStart = Math.Min(
                    cursorValidado.Length,
                    textoValidado.Length);
            }
            finally
            {
                _ajustandoTexto = false;
            }
        }

        // ==========================================
        // VALIDACIÓN DE PESO EN TIEMPO REAL
        // ==========================================

        private void ValidarPeso(TextBox sender)
        {
            if (_ajustandoTexto)
                return;

            string textoOriginal = sender.Text;
            int posicionAnterior = sender.SelectionStart;

            string resultado = "";
            bool separadorAgregado = false;

            foreach (char caracter in textoOriginal)
            {
                if (char.IsDigit(caracter))
                {
                    resultado += caracter;
                }
                else if ((caracter == '.' || caracter == ',') &&
                         !separadorAgregado &&
                         resultado.Length > 0)
                {
                    resultado += '.';
                    separadorAgregado = true;
                }
            }

            string[] partes = resultado.Split('.');

            // Se permiten cuatro dígitos para admitir hasta 1000 kg.
            if (partes[0].Length > 4)
                partes[0] = partes[0].Substring(0, 4);

            if (partes.Length > 1)
            {
                if (partes[1].Length > 2)
                    partes[1] = partes[1].Substring(0, 2);

                resultado = partes[0] + "." + partes[1];
            }
            else
            {
                resultado = partes[0];
            }

            if (textoOriginal == resultado)
                return;

            _ajustandoTexto = true;

            try
            {
                sender.Text = resultado;

                sender.SelectionStart = Math.Min(
                    posicionAnterior,
                    resultado.Length);
            }
            finally
            {
                _ajustandoTexto = false;
            }
        }

        // ==========================================
        // EVENTOS DE VALIDACIÓN
        // ==========================================

        private void TxtPeso_TextChanging(
            TextBox sender,
            TextBoxTextChangingEventArgs args)
        {
            ValidarPeso(sender);
        }

        private void TxtMotivo_TextChanging(
            TextBox sender,
            TextBoxTextChangingEventArgs args)
        {
            ValidarCampo(sender);
        }

        private void TxtSintomas_TextChanging(
            TextBox sender,
            TextBoxTextChangingEventArgs args)
        {
            ValidarCampo(sender);
        }

        private void TxtDiagnostico_TextChanging(
            TextBox sender,
            TextBoxTextChangingEventArgs args)
        {
            ValidarCampo(sender);
        }

        private void TxtTratamiento_TextChanging(
            TextBox sender,
            TextBoxTextChangingEventArgs args)
        {
            ValidarCampo(sender);
        }

        private void TxtAlergias_TextChanging(
            TextBox sender,
            TextBoxTextChangingEventArgs args)
        {
            ValidarCampo(sender);
        }

        private void TxtAntecedentes_TextChanging(
            TextBox sender,
            TextBoxTextChangingEventArgs args)
        {
            ValidarCampo(sender);
        }

        private void TxtObservaciones_TextChanging(
            TextBox sender,
            TextBoxTextChangingEventArgs args)
        {
            ValidarCampo(sender);
        }

        private void TxtProducto_TextChanging(
            TextBox sender,
            TextBoxTextChangingEventArgs args)
        {
            ValidarCampo(sender);
        }

        private void TxtObservacionesCartilla_TextChanging(
            TextBox sender,
            TextBoxTextChangingEventArgs args)
        {
            ValidarCampo(sender);
        }

        // ==========================================
        // MENSAJES
        // ==========================================

        private void MostrarMensaje(
            string mensaje,
            bool esError)
        {
            TxtMensaje.Text = mensaje;

            TxtMensaje.Foreground =
                new SolidColorBrush(
                    esError ? ColorError : ColorExito);

            TxtMensaje.Visibility = Visibility.Visible;
        }

        // ==========================================
        // NAVEGACIÓN
        // ==========================================

        private void BtnConsultarHistorial_Click(
            object sender,
            RoutedEventArgs e)
        {
            MainWindow.Current?.SeleccionarPantalla(
                "ConsultarHistorial");
        }
    }
}
