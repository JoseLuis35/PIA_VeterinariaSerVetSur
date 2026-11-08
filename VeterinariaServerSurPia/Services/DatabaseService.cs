
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;

namespace VeterinariaServerSurPia.Services
{
    public sealed class MascotaDb
    {
        public string Id { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string Especie { get; set; } = "";
        public string Raza { get; set; } = "";
        public string Edad { get; set; } = "";
        public string Sexo { get; set; } = "";
        public string Peso { get; set; } = "";
        public string IdPropietario { get; set; } = "";
    }

    public sealed class ObservacionClinicaDb
    {
        public int Id { get; set; }
        public string IdMascota { get; set; } = "";
        public string NombreMascota { get; set; } = "";
        public string Especie { get; set; } = "";
        public DateTime Fecha { get; set; }
        public string Observacion { get; set; } = "";
        public string Diagnostico { get; set; } = "";
    }

    // Antecedente clínico unificado: viene de las observaciones clínicas
    // o del historial médico de cualquier mascota de la misma especie.
    public sealed class AntecedenteClinicoDb
    {
        public string Fuente { get; set; } = "";
        public string IdMascota { get; set; } = "";
        public string NombreMascota { get; set; } = "";
        public string Especie { get; set; } = "";
        public string Raza { get; set; } = "";
        public string Edad { get; set; } = "";
        public string Sexo { get; set; } = "";
        public DateTime Fecha { get; set; }
        public string Texto { get; set; } = "";
        public string Diagnostico { get; set; } = "";
        public string Tratamiento { get; set; } = "";
        public string Alergias { get; set; } = "";
    }

    public class DatabaseService
    {
        private readonly string _databasePath;
        private readonly string _connectionString;

        private const string MigracionInicial =
            "MIGRACION_ARCHIVOS_LEGADOS_V1";

        public DatabaseService()
        {
            string localFolder = ApplicationData.Current.LocalFolder.Path;

            _databasePath = Path.Combine(
                localFolder,
                "vetsmart_ai.db");

            CopiarBaseIncluidaSiNoExiste();

            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = _databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared,
                ForeignKeys = true
            };

            _connectionString = builder.ToString();
        }

        // =========================================================
        // BASE DE DATOS INCLUIDA EN EL PROYECTO (Database\vetsmart_ai.db)
        // =========================================================

        // La primera vez que se abre la app, se copia la base incluida en el
        // proyecto a la carpeta de datos de la aplicación (que sí es
        // escribible). Si ya existe una base ahí, NUNCA se sobrescribe.
        private void CopiarBaseIncluidaSiNoExiste()
        {
            try
            {
                if (File.Exists(_databasePath))
                    return;

                string baseIncluida = Path.Combine(
                    AppContext.BaseDirectory,
                    "Database",
                    "vetsmart_ai.db");

                if (File.Exists(baseIncluida))
                {
                    File.Copy(baseIncluida, _databasePath, overwrite: false);
                }
            }
            catch (Exception ex)
            {
                // Si no se puede copiar, SQLite crea una base nueva
                // con las mismas tablas al inicializar.
                Debug.WriteLine("No se pudo copiar la base incluida: " + ex.Message);
            }
        }

        // =========================================================
        // INICIALIZAR SQLITE
        // =========================================================

        public async Task InitializeAsync()
        {
            using var connection = await OpenConnectionAsync();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    PRAGMA journal_mode=WAL;

                    CREATE TABLE IF NOT EXISTS Clientes (
                        IdCliente TEXT PRIMARY KEY,
                        Nombre TEXT NOT NULL,
                        Telefono TEXT,
                        Correo TEXT,
                        Activo INTEGER NOT NULL DEFAULT 1,
                        FechaRegistro TEXT
                    );

                    CREATE TABLE IF NOT EXISTS Mascotas (
                        IdMascota TEXT PRIMARY KEY,
                        Nombre TEXT NOT NULL,
                        Especie TEXT,
                        Raza TEXT,
                        Edad TEXT,
                        Sexo TEXT,
                        Peso REAL,
                        IdCliente TEXT NOT NULL,
                        FOREIGN KEY (IdCliente)
                            REFERENCES Clientes(IdCliente)
                            ON UPDATE CASCADE ON DELETE RESTRICT
                    );

                    CREATE TABLE IF NOT EXISTS Citas (
                        IdCita TEXT PRIMARY KEY,
                        IdMascota TEXT NOT NULL,
                        Fecha TEXT NOT NULL,
                        Hora TEXT,
                        Servicio TEXT,
                        Observaciones TEXT,
                        FOREIGN KEY (IdMascota)
                            REFERENCES Mascotas(IdMascota)
                            ON UPDATE CASCADE ON DELETE RESTRICT
                    );

                    CREATE TABLE IF NOT EXISTS HistorialMedico (
                        IdRegistro TEXT PRIMARY KEY,
                        IdMascota TEXT NOT NULL,
                        Fecha TEXT NOT NULL,
                        Peso REAL,
                        Motivo TEXT,
                        Sintomas TEXT,
                        Diagnostico TEXT,
                        Tratamiento TEXT,
                        Alergias TEXT,
                        Antecedentes TEXT,
                        Observaciones TEXT,
                        FOREIGN KEY (IdMascota)
                            REFERENCES Mascotas(IdMascota)
                            ON UPDATE CASCADE ON DELETE RESTRICT
                    );

                    CREATE TABLE IF NOT EXISTS CartillaMedica (
                        IdRegistro TEXT PRIMARY KEY,
                        IdMascota TEXT NOT NULL,
                        Tipo TEXT,
                        Producto TEXT,
                        FechaAplicacion TEXT,
                        ProximaFecha TEXT,
                        Observaciones TEXT,
                        FOREIGN KEY (IdMascota)
                            REFERENCES Mascotas(IdMascota)
                            ON UPDATE CASCADE ON DELETE RESTRICT
                    );

                    CREATE TABLE IF NOT EXISTS ObservacionesClinicas (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        IdMascota TEXT NOT NULL,
                        NombreMascota TEXT,
                        Especie TEXT,
                        Fecha TEXT NOT NULL,
                        Observacion TEXT NOT NULL,
                        Diagnostico TEXT,
                        FOREIGN KEY (IdMascota)
                            REFERENCES Mascotas(IdMascota)
                            ON UPDATE CASCADE ON DELETE CASCADE
                    );

                    CREATE TABLE IF NOT EXISTS Migraciones (
                        Clave TEXT PRIMARY KEY,
                        Fecha TEXT NOT NULL
                    );

                    CREATE INDEX IF NOT EXISTS IX_Mascotas_IdCliente
                        ON Mascotas(IdCliente);

                    CREATE INDEX IF NOT EXISTS IX_Citas_IdMascota
                        ON Citas(IdMascota);

                    CREATE INDEX IF NOT EXISTS IX_Historial_IdMascota
                        ON HistorialMedico(IdMascota);

                    CREATE INDEX IF NOT EXISTS IX_Cartilla_IdMascota
                        ON CartillaMedica(IdMascota);

                    CREATE INDEX IF NOT EXISTS IX_Observaciones_IdMascota
                        ON ObservacionesClinicas(IdMascota);
                ";

                await command.ExecuteNonQueryAsync();
            }

            await EjecutarMigracionInicialAsync(connection);

            Debug.WriteLine("Base SQLite: " + _databasePath);
        }

        private async Task EjecutarMigracionInicialAsync(
            SqliteConnection connection)
        {
            using var verificar = connection.CreateCommand();

            verificar.CommandText = @"
                SELECT COUNT(*)
                FROM Migraciones
                WHERE Clave = $clave;";

            verificar.Parameters.AddWithValue(
                "$clave",
                MigracionInicial);

            long existe = (long)(await verificar.ExecuteScalarAsync() ?? 0L);

            if (existe > 0)
                return;

            Debug.WriteLine("Iniciando migración de archivos antiguos...");

            using var transaction = connection.BeginTransaction();

            try
            {
                await ImportarClientesAsync(connection, transaction);
                await ImportarMascotasAsync(connection, transaction);
                await ImportarCitasAsync(connection, transaction);
                await ImportarHistorialAsync(connection, transaction);
                await ImportarCartillaAsync(connection, transaction);
                await ImportarConocimientoJsonAsync(connection, transaction);

                using var marcar = connection.CreateCommand();
                marcar.Transaction = transaction;

                marcar.CommandText = @"
                    INSERT INTO Migraciones (Clave, Fecha)
                    VALUES ($clave, $fecha);";

                marcar.Parameters.AddWithValue(
                    "$clave",
                    MigracionInicial);

                marcar.Parameters.AddWithValue(
                    "$fecha",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                await marcar.ExecuteNonQueryAsync();

                transaction.Commit();

                Debug.WriteLine("Migración inicial completada.");
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        // =========================================================
        // MIGRACIÓN DE ARCHIVOS ANTIGUOS
        // =========================================================

        private static string RutaLegada(string nombre)
        {
            return Path.Combine(AppContext.BaseDirectory, nombre);
        }

        private static string[] Separar(string linea, char separador)
        {
            return linea.Split(separador);
        }

        private static object PesoSql(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return DBNull.Value;

            if (double.TryParse(
                texto.Trim(),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out double peso))
            {
                return peso;
            }

            if (double.TryParse(
                texto.Trim(),
                NumberStyles.Any,
                CultureInfo.GetCultureInfo("es-MX"),
                out peso))
            {
                return peso;
            }

            return DBNull.Value;
        }

        private async Task ImportarClientesAsync(
            SqliteConnection connection,
            SqliteTransaction transaction)
        {
            string ruta = RutaLegada("clientes.txt");

            if (!File.Exists(ruta))
                return;

            foreach (string linea in File.ReadLines(ruta))
            {
                if (string.IsNullOrWhiteSpace(linea))
                    continue;

                string[] p = Separar(linea, ',');

                if (p.Length < 4 ||
                    string.IsNullOrWhiteSpace(p[0]) ||
                    string.IsNullOrWhiteSpace(p[1]))
                    continue;

                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;

                cmd.CommandText = @"
                    INSERT OR IGNORE INTO Clientes
                    (IdCliente, Nombre, Telefono, Correo, Activo, FechaRegistro)
                    VALUES ($id, $nombre, $telefono, $correo, 1, $fecha);";

                cmd.Parameters.AddWithValue("$id", p[0].Trim());
                cmd.Parameters.AddWithValue("$nombre", p[1].Trim());
                cmd.Parameters.AddWithValue("$telefono", p[2].Trim());
                cmd.Parameters.AddWithValue("$correo", p[3].Trim());
                cmd.Parameters.AddWithValue(
                    "$fecha",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task ImportarMascotasAsync(
            SqliteConnection connection,
            SqliteTransaction transaction)
        {
            string ruta = RutaLegada("mascotas.txt");

            if (!File.Exists(ruta))
                return;

            foreach (string linea in File.ReadLines(ruta))
            {
                if (string.IsNullOrWhiteSpace(linea))
                    continue;

                string[] p = Separar(linea, ',');

                if (p.Length < 8 ||
                    string.IsNullOrWhiteSpace(p[0]) ||
                    string.IsNullOrWhiteSpace(p[1]) ||
                    string.IsNullOrWhiteSpace(p[7]))
                    continue;

                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;

                cmd.CommandText = @"
                    INSERT OR IGNORE INTO Mascotas
                    (IdMascota, Nombre, Especie, Raza, Edad, Sexo, Peso, IdCliente)
                    SELECT $id, $nombre, $especie, $raza, $edad, $sexo, $peso, $cliente
                    WHERE EXISTS (
                        SELECT 1 FROM Clientes WHERE IdCliente = $cliente
                    );";

                cmd.Parameters.AddWithValue("$id", p[0].Trim());
                cmd.Parameters.AddWithValue("$nombre", p[1].Trim());
                cmd.Parameters.AddWithValue("$especie", p[2].Trim());
                cmd.Parameters.AddWithValue("$raza", p[3].Trim());
                cmd.Parameters.AddWithValue("$edad", p[4].Trim());
                cmd.Parameters.AddWithValue("$sexo", p[5].Trim());
                cmd.Parameters.AddWithValue("$peso", PesoSql(p[6]));
                cmd.Parameters.AddWithValue("$cliente", p[7].Trim());

                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task ImportarCitasAsync(
            SqliteConnection connection,
            SqliteTransaction transaction)
        {
            string ruta = RutaLegada("citas.txt");

            if (!File.Exists(ruta))
                return;

            foreach (string linea in File.ReadLines(ruta))
            {
                if (string.IsNullOrWhiteSpace(linea))
                    continue;

                string[] p = Separar(linea, ',');

                if (p.Length < 4)
                    continue;

                string id = p[0].Trim();
                string idMascota;
                string fecha;
                string hora = "";
                string servicio;
                string observaciones = "";

                if (p.Length == 4)
                {
                    fecha = NormalizarFecha(p[1].Trim());
                    servicio = p[2].Trim();
                    idMascota = p[3].Trim();
                }
                else
                {
                    if (p.Length < 5)
                        continue;

                    idMascota = p[1].Trim();
                    fecha = NormalizarFecha(p[2].Trim());
                    hora = p[3].Trim();
                    servicio = p[4].Trim();

                    if (p.Length > 5)
                        observaciones = string.Join(",", p.Skip(5)).Trim();
                }

                if (string.IsNullOrWhiteSpace(id) ||
                    string.IsNullOrWhiteSpace(idMascota) ||
                    string.IsNullOrWhiteSpace(fecha))
                    continue;

                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;

                cmd.CommandText = @"
                    INSERT OR IGNORE INTO Citas
                    (IdCita, IdMascota, Fecha, Hora, Servicio, Observaciones)
                    SELECT $id, $mascota, $fecha, $hora, $servicio, $observaciones
                    WHERE EXISTS (
                        SELECT 1 FROM Mascotas WHERE IdMascota = $mascota
                    );";

                cmd.Parameters.AddWithValue("$id", id);
                cmd.Parameters.AddWithValue("$mascota", idMascota);
                cmd.Parameters.AddWithValue("$fecha", fecha);
                cmd.Parameters.AddWithValue("$hora", hora);
                cmd.Parameters.AddWithValue("$servicio", servicio);
                cmd.Parameters.AddWithValue("$observaciones", observaciones);

                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task ImportarHistorialAsync(
            SqliteConnection connection,
            SqliteTransaction transaction)
        {
            string ruta = RutaLegada("historial_medico.txt");

            if (!File.Exists(ruta))
                return;

            foreach (string linea in File.ReadLines(ruta))
            {
                if (string.IsNullOrWhiteSpace(linea))
                    continue;

                string[] p = Separar(linea, '|');

                if (p.Length < 11 ||
                    string.IsNullOrWhiteSpace(p[0]) ||
                    string.IsNullOrWhiteSpace(p[1]))
                    continue;

                string fecha = NormalizarFecha(p[2].Trim());

                if (string.IsNullOrWhiteSpace(fecha))
                    continue;

                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;

                cmd.CommandText = @"
                    INSERT OR IGNORE INTO HistorialMedico
                    (IdRegistro, IdMascota, Fecha, Peso, Motivo, Sintomas,
                     Diagnostico, Tratamiento, Alergias, Antecedentes, Observaciones)
                    SELECT $id, $mascota, $fecha, $peso, $motivo, $sintomas,
                           $diagnostico, $tratamiento, $alergias, $antecedentes,
                           $observaciones
                    WHERE EXISTS (
                        SELECT 1 FROM Mascotas WHERE IdMascota = $mascota
                    );";

                cmd.Parameters.AddWithValue("$id", p[0].Trim());
                cmd.Parameters.AddWithValue("$mascota", p[1].Trim());
                cmd.Parameters.AddWithValue("$fecha", fecha);
                cmd.Parameters.AddWithValue("$peso", PesoSql(p[3]));
                cmd.Parameters.AddWithValue("$motivo", p[4]);
                cmd.Parameters.AddWithValue("$sintomas", p[5]);
                cmd.Parameters.AddWithValue("$diagnostico", p[6]);
                cmd.Parameters.AddWithValue("$tratamiento", p[7]);
                cmd.Parameters.AddWithValue("$alergias", p[8]);
                cmd.Parameters.AddWithValue("$antecedentes", p[9]);
                cmd.Parameters.AddWithValue("$observaciones", p[10]);

                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task ImportarCartillaAsync(
            SqliteConnection connection,
            SqliteTransaction transaction)
        {
            string ruta = RutaLegada("cartilla_medica.txt");

            if (!File.Exists(ruta))
                return;

            foreach (string linea in File.ReadLines(ruta))
            {
                if (string.IsNullOrWhiteSpace(linea))
                    continue;

                string[] p = Separar(linea, '|');

                if (p.Length < 7 ||
                    string.IsNullOrWhiteSpace(p[0]) ||
                    string.IsNullOrWhiteSpace(p[1]))
                    continue;

                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;

                cmd.CommandText = @"
                    INSERT OR IGNORE INTO CartillaMedica
                    (IdRegistro, IdMascota, Tipo, Producto, FechaAplicacion,
                     ProximaFecha, Observaciones)
                    SELECT $id, $mascota, $tipo, $producto, $aplicacion,
                           $proxima, $observaciones
                    WHERE EXISTS (
                        SELECT 1 FROM Mascotas WHERE IdMascota = $mascota
                    );";

                cmd.Parameters.AddWithValue("$id", p[0].Trim());
                cmd.Parameters.AddWithValue("$mascota", p[1].Trim());
                cmd.Parameters.AddWithValue("$tipo", p[2]);
                cmd.Parameters.AddWithValue("$producto", p[3]);
                cmd.Parameters.AddWithValue(
                    "$aplicacion",
                    NormalizarFechaConservandoOriginal(p[4].Trim()));
                cmd.Parameters.AddWithValue(
                    "$proxima",
                    NormalizarFechaConservandoOriginal(p[5].Trim()));
                cmd.Parameters.AddWithValue("$observaciones", p[6]);

                await cmd.ExecuteNonQueryAsync();
            }
        }

        private sealed class ObservacionLegada
        {
            public string IdMascota { get; set; } = "";
            public string NombreMascota { get; set; } = "";
            public string Especie { get; set; } = "";
            public DateTime Fecha { get; set; }
            public string Observacion { get; set; } = "";
            public string Diagnostico { get; set; } = "";
        }

        private async Task ImportarConocimientoJsonAsync(
            SqliteConnection connection,
            SqliteTransaction transaction)
        {
            string ruta = RutaLegada("vetsmart_conocimiento.json");

            if (!File.Exists(ruta))
                return;

            try
            {
                string json = await File.ReadAllTextAsync(ruta);

                var registros =
                    JsonSerializer.Deserialize<List<ObservacionLegada>>(json)
                    ?? new List<ObservacionLegada>();

                foreach (var registro in registros)
                {
                    if (string.IsNullOrWhiteSpace(registro.IdMascota) ||
                        string.IsNullOrWhiteSpace(registro.Observacion))
                        continue;

                    using var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;

                    cmd.CommandText = @"
                        INSERT INTO ObservacionesClinicas
                        (IdMascota, NombreMascota, Especie, Fecha,
                         Observacion, Diagnostico)
                        SELECT $mascota, $nombre, $especie, $fecha,
                               $observacion, $diagnostico
                        WHERE EXISTS (
                            SELECT 1 FROM Mascotas WHERE IdMascota = $mascota
                        )
                        AND NOT EXISTS (
                            SELECT 1 FROM ObservacionesClinicas
                            WHERE IdMascota = $mascota
                              AND Fecha = $fecha
                              AND Observacion = $observacion
                        );";

                    cmd.Parameters.AddWithValue(
                        "$mascota",
                        registro.IdMascota.Trim());

                    cmd.Parameters.AddWithValue(
                        "$nombre",
                        registro.NombreMascota ?? "");

                    cmd.Parameters.AddWithValue(
                        "$especie",
                        registro.Especie ?? "");

                    cmd.Parameters.AddWithValue(
                        "$fecha",
                        registro.Fecha == default
                            ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                            : registro.Fecha.ToString("yyyy-MM-dd HH:mm:ss"));

                    cmd.Parameters.AddWithValue(
                        "$observacion",
                        registro.Observacion);

                    cmd.Parameters.AddWithValue(
                        "$diagnostico",
                        registro.Diagnostico ?? "");

                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch (JsonException ex)
            {
                Debug.WriteLine(
                    "No se pudo migrar vetsmart_conocimiento.json: " +
                    ex.Message);

                throw;
            }
        }

        // =========================================================
        // MASCOTAS
        // =========================================================

        public async Task<List<MascotaDb>> ObtenerMascotasAsync()
        {
            var resultado = new List<MascotaDb>();

            using var connection = await OpenConnectionAsync();
            using var command = connection.CreateCommand();

            command.CommandText = @"
                SELECT IdMascota, Nombre, Especie, Raza, Edad, Sexo,
                       Peso, IdCliente
                FROM Mascotas
                ORDER BY Nombre COLLATE NOCASE;";

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                resultado.Add(new MascotaDb
                {
                    Id = Valor(reader, 0),
                    Nombre = Valor(reader, 1),
                    Especie = Valor(reader, 2),
                    Raza = Valor(reader, 3),
                    Edad = Valor(reader, 4),
                    Sexo = Valor(reader, 5),
                    Peso = Valor(reader, 6),
                    IdPropietario = Valor(reader, 7)
                });
            }

            return resultado;
        }

        // =========================================================
        // OBSERVACIONES CLÍNICAS
        // =========================================================

        public async Task GuardarObservacionClinicaAsync(
            string idMascota,
            string nombreMascota,
            string especie,
            string observacion,
            string diagnostico)
        {
            using var connection = await OpenConnectionAsync();
            using var command = connection.CreateCommand();

            command.CommandText = @"
                INSERT INTO ObservacionesClinicas
                (IdMascota, NombreMascota, Especie, Fecha,
                 Observacion, Diagnostico)
                SELECT $id, $nombre, $especie, $fecha, $observacion, $diagnostico
                WHERE EXISTS (
                    SELECT 1 FROM Mascotas WHERE IdMascota = $id
                );";

            command.Parameters.AddWithValue("$id", idMascota);
            command.Parameters.AddWithValue("$nombre", nombreMascota);
            command.Parameters.AddWithValue("$especie", especie);
            command.Parameters.AddWithValue(
                "$fecha",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            command.Parameters.AddWithValue("$observacion", observacion);
            command.Parameters.AddWithValue("$diagnostico", diagnostico);

            int filas = await command.ExecuteNonQueryAsync();

            if (filas == 0)
            {
                throw new InvalidOperationException(
                    "No se guardó la observación porque la mascota no existe en SQLite.");
            }
        }

        // Elimina una observación clínica. Se filtra también por mascota
        // para no borrar por error la observación de otra.
        public async Task<bool> EliminarObservacionClinicaAsync(
            int idObservacion,
            string idMascota)
        {
            using var connection = await OpenConnectionAsync();
            using var command = connection.CreateCommand();

            command.CommandText = @"
                DELETE FROM ObservacionesClinicas
                WHERE Id = $id AND IdMascota = $mascota;";

            command.Parameters.AddWithValue("$id", idObservacion);
            command.Parameters.AddWithValue("$mascota", idMascota);

            int filas = await command.ExecuteNonQueryAsync();

            return filas > 0;
        }

        public async Task<List<ObservacionClinicaDb>>
            ObtenerObservacionesClinicasAsync(string? idMascota = null)
        {
            var resultado = new List<ObservacionClinicaDb>();

            using var connection = await OpenConnectionAsync();
            using var command = connection.CreateCommand();

            if (string.IsNullOrWhiteSpace(idMascota))
            {
                command.CommandText = @"
                    SELECT Id, IdMascota, NombreMascota, Especie,
                           Fecha, Observacion, Diagnostico
                    FROM ObservacionesClinicas
                    ORDER BY Fecha DESC;";
            }
            else
            {
                command.CommandText = @"
                    SELECT Id, IdMascota, NombreMascota, Especie,
                           Fecha, Observacion, Diagnostico
                    FROM ObservacionesClinicas
                    WHERE IdMascota = $id
                    ORDER BY Fecha DESC;";

                command.Parameters.AddWithValue("$id", idMascota);
            }

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                DateTime.TryParse(
                    Valor(reader, 4),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime fecha);

                resultado.Add(new ObservacionClinicaDb
                {
                    Id = reader.GetInt32(0),
                    IdMascota = Valor(reader, 1),
                    NombreMascota = Valor(reader, 2),
                    Especie = Valor(reader, 3),
                    Fecha = fecha,
                    Observacion = Valor(reader, 5),
                    Diagnostico = Valor(reader, 6)
                });
            }

            return resultado;
        }

        // NUEVO: OBTENER OBSERVACIONES POR ESPECIE

        public async Task<List<ObservacionClinicaDb>>
            ObtenerObservacionesPorEspecieAsync(string especie)
        {
            var resultado = new List<ObservacionClinicaDb>();

            if (string.IsNullOrWhiteSpace(especie))
                return resultado;

            using var connection = await OpenConnectionAsync();
            using var command = connection.CreateCommand();

            command.CommandText = @"
                SELECT Id, IdMascota, NombreMascota, Especie,
                       Fecha, Observacion, Diagnostico
                FROM ObservacionesClinicas
                WHERE TRIM(Especie) = TRIM($especie) COLLATE NOCASE
                ORDER BY Fecha DESC;";

            command.Parameters.AddWithValue(
                "$especie",
                especie.Trim());

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                DateTime.TryParse(
                    Valor(reader, 4),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime fecha);

                resultado.Add(new ObservacionClinicaDb
                {
                    Id = reader.GetInt32(0),
                    IdMascota = Valor(reader, 1),
                    NombreMascota = Valor(reader, 2),
                    Especie = Valor(reader, 3),
                    Fecha = fecha,
                    Observacion = Valor(reader, 5),
                    Diagnostico = Valor(reader, 6)
                });
            }

            return resultado;
        }

        // =========================================================
        // ANTECEDENTES CLÍNICOS PARA BÚSQUEDA DE CASOS SIMILARES
        // (observaciones clínicas + historial médico de TODAS las mascotas)
        // =========================================================

        public async Task<List<AntecedenteClinicoDb>>
            ObtenerTodosLosAntecedentesClinicosAsync()
        {
            var resultado = new List<AntecedenteClinicoDb>();

            using var connection = await OpenConnectionAsync();
            using var command = connection.CreateCommand();

            command.CommandText = @"
                SELECT 'Observación clínica' AS Fuente,
                       o.IdMascota,
                       COALESCE(NULLIF(TRIM(o.NombreMascota), ''), m.Nombre),
                       COALESCE(NULLIF(TRIM(o.Especie), ''), m.Especie),
                       m.Raza, m.Edad, m.Sexo,
                       o.Fecha,
                       o.Observacion,
                       o.Diagnostico,
                       '' AS Tratamiento,
                       '' AS Alergias
                FROM ObservacionesClinicas o
                LEFT JOIN Mascotas m ON m.IdMascota = o.IdMascota

                UNION ALL

                SELECT 'Historial médico',
                       h.IdMascota,
                       m.Nombre,
                       m.Especie,
                       m.Raza, m.Edad, m.Sexo,
                       h.Fecha,
                       TRIM(COALESCE(h.Motivo, '') || '. ' ||
                            COALESCE(h.Sintomas, '') || '. ' ||
                            COALESCE(h.Observaciones, '')),
                       h.Diagnostico,
                       h.Tratamiento,
                       h.Alergias
                FROM HistorialMedico h
                INNER JOIN Mascotas m ON m.IdMascota = h.IdMascota

                ORDER BY 8 DESC;";

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                DateTime.TryParse(
                    TextoOpcional(reader, 7),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime fecha);

                resultado.Add(new AntecedenteClinicoDb
                {
                    Fuente = TextoOpcional(reader, 0),
                    IdMascota = TextoOpcional(reader, 1),
                    NombreMascota = TextoOpcional(reader, 2),
                    Especie = TextoOpcional(reader, 3),
                    Raza = TextoOpcional(reader, 4),
                    Edad = TextoOpcional(reader, 5),
                    Sexo = TextoOpcional(reader, 6),
                    Fecha = fecha,
                    Texto = TextoOpcional(reader, 8),
                    Diagnostico = TextoOpcional(reader, 9),
                    Tratamiento = TextoOpcional(reader, 10),
                    Alergias = TextoOpcional(reader, 11)
                });
            }

            return resultado;
        }

        public async Task<string> ObtenerNombreClienteAsync(
            string idCliente)
        {
            if (string.IsNullOrWhiteSpace(idCliente))
                return "";

            using var connection = await OpenConnectionAsync();
            using var command = connection.CreateCommand();

            command.CommandText =
                "SELECT Nombre FROM Clientes WHERE IdCliente = $id LIMIT 1;";

            command.Parameters.AddWithValue("$id", idCliente);

            object? valor = await command.ExecuteScalarAsync();

            return Convert.ToString(valor, CultureInfo.InvariantCulture)
                ?? "";
        }

        // Igual que Valor(), pero devuelve "" cuando no hay dato.
        private static string TextoOpcional(
            SqliteDataReader reader,
            int indice)
        {
            if (reader.IsDBNull(indice))
                return "";

            return (Convert.ToString(
                reader.GetValue(indice),
                CultureInfo.InvariantCulture) ?? "").Trim();
        }

        // =========================================================
        // CONTEXTO PARA VETSMART AI
        // =========================================================

        // Define qué secciones del expediente hacen falta para responder
        // la pregunta. Menos datos = respuesta más rápida y más enfocada.
        private readonly record struct PlanContexto(
            bool Citas,
            bool Historial,
            bool Cartilla,
            bool Observaciones,
            int LimiteCitas,
            int LimiteHistorial,
            int LimiteCartilla,
            int LimiteObservaciones);

        private static string NormalizarBusqueda(string texto)
        {
            string descompuesto = texto
                .ToLowerInvariant()
                .Normalize(NormalizationForm.FormD);

            var sb = new StringBuilder(descompuesto.Length);

            foreach (char c in descompuesto)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) !=
                    UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        private static bool ContieneAlguna(
            string texto,
            params string[] palabras)
        {
            foreach (string palabra in palabras)
            {
                if (texto.Contains(palabra))
                    return true;
            }

            return false;
        }

        private static PlanContexto CrearPlanContexto(string? pregunta)
        {
            // Sin pregunta: comportamiento completo de siempre.
            if (string.IsNullOrWhiteSpace(pregunta))
            {
                return new PlanContexto(
                    true, true, true, true, 15, 15, 20, int.MaxValue);
            }

            string n = NormalizarBusqueda(pregunta);

            bool citas = ContieneAlguna(n,
                "cita", "agenda", "programad", "horario", "proxima consulta");

            bool cartilla = ContieneAlguna(n,
                "vacun", "desparasit", "cartilla", "refuerzo",
                "antirrabic", "parvo", "moquillo", "rabia");

            bool historial = ContieneAlguna(n,
                "historial", "expediente", "diagnostic", "sintoma",
                "tratamiento", "alergi", "antecedente", "motivo",
                "enfermedad", "medicamento", "cirugia", "peso");

            bool observaciones = ContieneAlguna(n,
                "observacion", "comentario", "nota", "caso",
                "sintoma", "diagnostic", "antecedente", "le pasa",
                "padece", "evolucion", "mejoria");

            bool datosBasicos = ContieneAlguna(n,
                "raza", "especie", "edad", "sexo", "propietario",
                "dueno", "duena", "telefono", "cuantos anos");

            // Pregunta de dato básico: basta con la ficha de la mascota.
            if (!citas && !cartilla && !historial && !observaciones &&
                datosBasicos)
            {
                return new PlanContexto(
                    false, false, false, false, 0, 0, 0, 0);
            }

            // Pregunta clínica abierta ("¿qué podría tener?"):
            // historial, observaciones y cartilla, pocos registros.
            if (!citas && !cartilla && !historial && !observaciones)
            {
                return new PlanContexto(
                    false, true, true, true, 0, 6, 8, 6);
            }

            return new PlanContexto(
                citas, historial, cartilla, observaciones,
                6, 6, 10, 6);
        }

        public async Task<string> ObtenerContextoVeterinarioAsync(
            string? idMascota,
            string? pregunta = null)
        {
            PlanContexto plan = CrearPlanContexto(pregunta);

            using var connection = await OpenConnectionAsync();

            var contexto = new StringBuilder();

            contexto.AppendLine(
                "DATOS CONSULTADOS DIRECTAMENTE EN SQLITE.");

            contexto.AppendLine(
                "Utiliza únicamente los datos proporcionados. " +
                "Si algo no aparece, indica que no está registrado.");

            contexto.AppendLine();

            if (string.IsNullOrWhiteSpace(idMascota))
            {
                using var cmd = connection.CreateCommand();

                cmd.CommandText = @"
                    SELECT m.IdMascota, m.Nombre, m.Especie, m.Raza, c.Nombre
                    FROM Mascotas m
                    LEFT JOIN Clientes c ON c.IdCliente = m.IdCliente
                    ORDER BY m.Nombre
                    LIMIT 100;";

                using var reader = await cmd.ExecuteReaderAsync();

                contexto.AppendLine("MASCOTAS REGISTRADAS:");

                int cantidad = 0;

                while (await reader.ReadAsync())
                {
                    cantidad++;

                    contexto.AppendLine(
                        $"- ID: {Valor(reader, 0)} | Nombre: {Valor(reader, 1)} | " +
                        $"Especie: {Valor(reader, 2)} | Raza: {Valor(reader, 3)} | " +
                        $"Propietario: {Valor(reader, 4)}");
                }

                if (cantidad == 0)
                    contexto.AppendLine("No hay mascotas registradas.");

                return contexto.ToString();
            }

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT m.IdMascota, m.Nombre, m.Especie, m.Raza, m.Edad,
                           m.Sexo, m.Peso, c.IdCliente, c.Nombre
                    FROM Mascotas m
                    LEFT JOIN Clientes c ON c.IdCliente = m.IdCliente
                    WHERE m.IdMascota = $id
                    LIMIT 1;";

                cmd.Parameters.AddWithValue("$id", idMascota);

                using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    contexto.AppendLine(
                        $"No se encontró la mascota {idMascota} en SQLite.");

                    return contexto.ToString();
                }

                contexto.AppendLine("=== MASCOTA Y PROPIETARIO ===");
                contexto.AppendLine($"ID: {Valor(reader, 0)}");
                contexto.AppendLine($"Nombre: {Valor(reader, 1)}");
                contexto.AppendLine($"Especie: {Valor(reader, 2)}");
                contexto.AppendLine($"Raza: {Valor(reader, 3)}");
                contexto.AppendLine($"Edad: {Valor(reader, 4)}");
                contexto.AppendLine($"Sexo: {Valor(reader, 5)}");
                contexto.AppendLine($"Peso: {Valor(reader, 6)} kg");
                contexto.AppendLine($"ID propietario: {Valor(reader, 7)}");
                contexto.AppendLine($"Propietario: {Valor(reader, 8)}");
            }

            var omitidas = new List<string>();

            if (plan.Citas)
            {
                await AgregarSeccionAsync(
                    connection,
                    contexto,
                    "CITAS",
                    $@"SELECT Fecha, Hora, Servicio, Observaciones
                      FROM Citas
                      WHERE IdMascota = $id
                      ORDER BY Fecha DESC, Hora DESC
                      LIMIT {plan.LimiteCitas};",
                    idMascota,
                    4);
            }
            else
            {
                omitidas.Add("citas");
            }

            if (plan.Historial)
            {
                await AgregarSeccionAsync(
                    connection,
                    contexto,
                    "HISTORIAL MÉDICO",
                    $@"SELECT Fecha, Peso, Motivo, Sintomas, Diagnostico,
                             Tratamiento, Alergias, Antecedentes, Observaciones
                      FROM HistorialMedico
                      WHERE IdMascota = $id
                      ORDER BY Fecha DESC
                      LIMIT {plan.LimiteHistorial};",
                    idMascota,
                    9);
            }
            else
            {
                omitidas.Add("historial médico");
            }

            if (plan.Cartilla)
            {
                await AgregarSeccionAsync(
                    connection,
                    contexto,
                    "CARTILLA MÉDICA",
                    $@"SELECT Tipo, Producto, FechaAplicacion, ProximaFecha,
                             Observaciones
                      FROM CartillaMedica
                      WHERE IdMascota = $id
                      ORDER BY FechaAplicacion DESC
                      LIMIT {plan.LimiteCartilla};",
                    idMascota,
                    5);
            }
            else
            {
                omitidas.Add("cartilla médica");
            }

            if (plan.Observaciones)
            {
                var observaciones =
                    (await ObtenerObservacionesClinicasAsync(idMascota))
                        .Take(plan.LimiteObservaciones)
                        .ToList();

                contexto.AppendLine();
                contexto.AppendLine("=== OBSERVACIONES CLÍNICAS ===");

                if (observaciones.Count == 0)
                {
                    contexto.AppendLine(
                        "No hay observaciones clínicas registradas.");
                }
                else
                {
                    foreach (var o in observaciones)
                    {
                        contexto.AppendLine(
                            $"Fecha: {o.Fecha:dd/MM/yyyy HH:mm}; " +
                            $"Observación: {o.Observacion}; " +
                            $"Diagnóstico: {TextoONoRegistrado(o.Diagnostico)}.");
                    }
                }
            }
            else
            {
                omitidas.Add("observaciones clínicas");
            }

            if (omitidas.Count > 0)
            {
                contexto.AppendLine();
                contexto.AppendLine(
                    "SECCIONES NO CONSULTADAS PARA ESTA PREGUNTA: " +
                    string.Join(", ", omitidas) +
                    ". No afirmes que no existen registros en ellas.");
            }

            contexto.AppendLine();
            contexto.AppendLine(
                "No confundas ausencia de registro con ausencia de enfermedad, " +
                "alergia, vacuna o tratamiento. No deduzcas datos faltantes.");

            return contexto.ToString();
        }

        private static async Task AgregarSeccionAsync(
            SqliteConnection connection,
            StringBuilder contexto,
            string titulo,
            string consulta,
            string idMascota,
            int columnas)
        {
            contexto.AppendLine();
            contexto.AppendLine("=== " + titulo + " ===");

            using var cmd = connection.CreateCommand();

            cmd.CommandText = consulta;
            cmd.Parameters.AddWithValue("$id", idMascota);

            using var reader = await cmd.ExecuteReaderAsync();

            int cantidad = 0;

            while (await reader.ReadAsync())
            {
                cantidad++;

                var valores = new List<string>();

                for (int i = 0; i < columnas; i++)
                    valores.Add(Valor(reader, i));

                contexto.AppendLine(
                    $"Registro {cantidad}: " +
                    string.Join("; ", valores));
            }

            if (cantidad == 0)
                contexto.AppendLine("No hay registros guardados.");
        }

        // =========================================================
        // CONEXIÓN Y UTILIDADES
        // =========================================================

        private static string Valor(
            SqliteDataReader reader,
            int indice)
        {
            if (reader.IsDBNull(indice))
                return "No registrado";

            string valor = Convert.ToString(
                reader.GetValue(indice),
                CultureInfo.InvariantCulture) ?? "";

            return string.IsNullOrWhiteSpace(valor)
                ? "No registrado"
                : valor;
        }

        private static string TextoONoRegistrado(string? texto)
        {
            return string.IsNullOrWhiteSpace(texto)
                ? "No registrado"
                : texto;
        }

        private static string NormalizarFechaConservandoOriginal(
            string fecha)
        {
            if (string.IsNullOrWhiteSpace(fecha))
                return "";

            string normalizada = NormalizarFecha(fecha);

            return string.IsNullOrWhiteSpace(normalizada)
                ? fecha
                : normalizada;
        }

        private static string NormalizarFecha(string fecha)
        {
            if (string.IsNullOrWhiteSpace(fecha))
                return "";

            string[] formatos =
            {
                "yyyy-MM-dd",
                "dd/MM/yyyy",
                "d/M/yyyy",
                "yyyy-MM-dd HH:mm:ss",
                "dd/MM/yyyy HH:mm"
            };

            if (DateTime.TryParseExact(
                fecha,
                formatos,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime convertida))
            {
                return convertida.ToString(
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture);
            }

            if (DateTime.TryParse(
                fecha,
                CultureInfo.GetCultureInfo("es-MX"),
                DateTimeStyles.None,
                out convertida))
            {
                return convertida.ToString(
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture);
            }

            return "";
        }

        public async Task<SqliteConnection> OpenConnectionAsync()
        {
            var connection = new SqliteConnection(_connectionString);

            try
            {
                await connection.OpenAsync();
                return connection;
            }
            catch
            {
                await connection.DisposeAsync();
                throw;
            }
        }

        public string GetDatabasePath() => _databasePath;
    }
}
