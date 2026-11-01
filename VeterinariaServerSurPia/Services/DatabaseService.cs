
using Microsoft.Data.Sqlite;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;

namespace VeterinariaServerSurPia.Services
{
    public class DatabaseService
    {
        private readonly string _databasePath;
        private readonly string _connectionString;

        public DatabaseService()
        {
            string localFolder = ApplicationData.Current.LocalFolder.Path;
            _databasePath = Path.Combine(localFolder, "vetsmart_ai.db");

            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = _databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared,
                ForeignKeys = true
            };

            _connectionString = builder.ToString();
        }

        public async Task InitializeAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

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
                        FOREIGN KEY (IdCliente) REFERENCES Clientes(IdCliente)
                            ON UPDATE CASCADE ON DELETE RESTRICT
                    );

                    CREATE TABLE IF NOT EXISTS Citas (
                        IdCita TEXT PRIMARY KEY,
                        IdMascota TEXT NOT NULL,
                        Fecha TEXT NOT NULL,
                        Hora TEXT,
                        Servicio TEXT,
                        Observaciones TEXT,
                        FOREIGN KEY (IdMascota) REFERENCES Mascotas(IdMascota)
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
                        FOREIGN KEY (IdMascota) REFERENCES Mascotas(IdMascota)
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
                        FOREIGN KEY (IdMascota) REFERENCES Mascotas(IdMascota)
                            ON UPDATE CASCADE ON DELETE RESTRICT
                    );

                    CREATE INDEX IF NOT EXISTS IX_Mascotas_IdCliente ON Mascotas(IdCliente);
                    CREATE INDEX IF NOT EXISTS IX_Citas_IdMascota ON Citas(IdMascota);
                    CREATE INDEX IF NOT EXISTS IX_Historial_IdMascota ON HistorialMedico(IdMascota);
                    CREATE INDEX IF NOT EXISTS IX_Cartilla_IdMascota ON CartillaMedica(IdMascota);
                ";

                await command.ExecuteNonQueryAsync();
            }

            Debug.WriteLine("========== VETSMART AI 2.0: MIGRACIÓN SQLITE ==========");

            await SincronizarDatosDesdeArchivosAsync(connection);

            Debug.WriteLine("Base de datos: " + GetDatabasePath());
            Debug.WriteLine("========== MIGRACIÓN FINALIZADA ==========");
        }

        public async Task SincronizarDatosDesdeArchivosAsync()
        {
            using var connection = await OpenConnectionAsync();
            await SincronizarDatosDesdeArchivosAsync(connection);
        }

        private async Task SincronizarDatosDesdeArchivosAsync(
            SqliteConnection connection)
        {
            // Se revisan los TXT antes de cada consulta a la IA.
            // INSERT OR IGNORE agrega registros nuevos sin duplicar los existentes.
            await ImportarClientesExistentesAsync(connection);
            await ImportarMascotasExistentesAsync(connection);
            await ImportarCitasExistentesAsync(connection);
            await ImportarHistorialMedicoExistenteAsync(connection);
            await ImportarCartillaMedicaExistenteAsync(connection);
        }

        private string ObtenerRutaArchivo(string archivo)
        {
            return Path.Combine(AppContext.BaseDirectory, archivo);
        }

        private async Task ImportarClientesExistentesAsync(
            SqliteConnection connection)
        {
            string ruta = ObtenerRutaArchivo("clientes.txt");

            if (!File.Exists(ruta))
            {
                Debug.WriteLine("No se encontró clientes.txt: " + ruta);
                return;
            }

            int leidos = 0;
            int importados = 0;
            int invalidos = 0;

            using var transaction = connection.BeginTransaction();

            try
            {
                foreach (string linea in File.ReadLines(ruta))
                {
                    if (string.IsNullOrWhiteSpace(linea))
                        continue;

                    leidos++;

                    string[] p = linea.Split(',');

                    if (p.Length < 4 ||
                        string.IsNullOrWhiteSpace(p[0]) ||
                        string.IsNullOrWhiteSpace(p[1]))
                    {
                        invalidos++;
                        continue;
                    }

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

                    importados += await cmd.ExecuteNonQueryAsync();
                }

                transaction.Commit();

                Debug.WriteLine(
                    $"Clientes: leídos={leidos}, nuevos={importados}, inválidos={invalidos}.");
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private async Task ImportarMascotasExistentesAsync(
            SqliteConnection connection)
        {
            string ruta = ObtenerRutaArchivo("mascotas.txt");

            if (!File.Exists(ruta))
            {
                Debug.WriteLine("No se encontró mascotas.txt: " + ruta);
                return;
            }

            int leidos = 0;
            int importados = 0;
            int invalidos = 0;

            using var transaction = connection.BeginTransaction();

            try
            {
                foreach (string linea in File.ReadLines(ruta))
                {
                    if (string.IsNullOrWhiteSpace(linea))
                        continue;

                    leidos++;

                    string[] p = linea.Split(',');

                    if (p.Length < 8 ||
                        string.IsNullOrWhiteSpace(p[0]) ||
                        string.IsNullOrWhiteSpace(p[1]) ||
                        string.IsNullOrWhiteSpace(p[7]))
                    {
                        invalidos++;
                        continue;
                    }

                    object peso = DBNull.Value;
                    string pesoTexto = p[6].Trim();

                    if (!string.IsNullOrWhiteSpace(pesoTexto))
                    {
                        if (double.TryParse(
                            pesoTexto,
                            NumberStyles.Any,
                            CultureInfo.InvariantCulture,
                            out double valor))
                        {
                            peso = valor;
                        }
                        else
                        {
                            invalidos++;
                            Debug.WriteLine(
                                "Peso inválido para mascota: " + p[1].Trim());
                            continue;
                        }
                    }

                    using var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;

                    cmd.CommandText = @"
                        INSERT OR IGNORE INTO Mascotas
                        (IdMascota, Nombre, Especie, Raza, Edad, Sexo, Peso, IdCliente)
                        SELECT $id, $nombre, $especie, $raza, $edad, $sexo, $peso, $idCliente
                        WHERE EXISTS (
                            SELECT 1 FROM Clientes WHERE IdCliente = $idCliente
                        );";

                    cmd.Parameters.AddWithValue("$id", p[0].Trim());
                    cmd.Parameters.AddWithValue("$nombre", p[1].Trim());
                    cmd.Parameters.AddWithValue("$especie", p[2].Trim());
                    cmd.Parameters.AddWithValue("$raza", p[3].Trim());
                    cmd.Parameters.AddWithValue("$edad", p[4].Trim());
                    cmd.Parameters.AddWithValue("$sexo", p[5].Trim());
                    cmd.Parameters.AddWithValue("$peso", peso);
                    cmd.Parameters.AddWithValue("$idCliente", p[7].Trim());

                    int resultado = await cmd.ExecuteNonQueryAsync();

                    importados += resultado;

                    if (resultado == 0)
                    {
                        Debug.WriteLine(
                            $"Mascota no importada (ID duplicado o cliente inexistente): {p[0].Trim()}");
                    }
                }

                transaction.Commit();

                Debug.WriteLine(
                    $"Mascotas: leídas={leidos}, nuevas={importados}, inválidas={invalidos}.");
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private async Task ImportarCitasExistentesAsync(
            SqliteConnection connection)
        {
            string ruta = ObtenerRutaArchivo("citas.txt");

            if (!File.Exists(ruta))
            {
                Debug.WriteLine("No se encontró citas.txt: " + ruta);
                return;
            }

            int leidos = 0;
            int importados = 0;
            int invalidos = 0;

            using var transaction = connection.BeginTransaction();

            try
            {
                foreach (string linea in File.ReadLines(ruta))
                {
                    if (string.IsNullOrWhiteSpace(linea))
                        continue;

                    leidos++;

                    string[] p = linea.Split(',');

                    if (p.Length < 4)
                    {
                        invalidos++;
                        continue;
                    }

                    string id = p[0].Trim();
                    string idMascota;
                    string fecha;
                    string hora = "";
                    string servicio = "";
                    string observaciones = "";

                    // Formato antiguo: ID, fecha, servicio, ID mascota.
                    // Formato actual: ID, ID mascota, fecha, hora, servicio, observaciones.
                    if (p.Length == 4)
                    {
                        fecha = NormalizarFecha(p[1].Trim());
                        servicio = p[2].Trim();
                        idMascota = p[3].Trim();
                    }
                    else
                    {
                        idMascota = p[1].Trim();
                        fecha = NormalizarFecha(p[2].Trim());
                        hora = p[3].Trim();
                        servicio = p[4].Trim();
                        observaciones = p.Length >= 6
                            ? string.Join(",", p.Skip(5)).Trim()
                            : "";
                    }

                    if (string.IsNullOrWhiteSpace(id) ||
                        string.IsNullOrWhiteSpace(idMascota) ||
                        string.IsNullOrWhiteSpace(fecha))
                    {
                        invalidos++;
                        continue;
                    }

                    using var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;

                    cmd.CommandText = @"
                        INSERT OR IGNORE INTO Citas
                        (IdCita, IdMascota, Fecha, Hora, Servicio, Observaciones)
                        SELECT $id, $idMascota, $fecha, $hora, $servicio, $observaciones
                        WHERE EXISTS (
                            SELECT 1 FROM Mascotas WHERE IdMascota = $idMascota
                        );";

                    cmd.Parameters.AddWithValue("$id", id);
                    cmd.Parameters.AddWithValue("$idMascota", idMascota);
                    cmd.Parameters.AddWithValue("$fecha", fecha);
                    cmd.Parameters.AddWithValue("$hora", hora);
                    cmd.Parameters.AddWithValue("$servicio", servicio);
                    cmd.Parameters.AddWithValue("$observaciones", observaciones);

                    int resultado = await cmd.ExecuteNonQueryAsync();

                    importados += resultado;

                    if (resultado == 0)
                    {
                        Debug.WriteLine(
                            "Cita omitida (ID duplicado o mascota inexistente): " + id);
                    }
                }

                transaction.Commit();

                Debug.WriteLine(
                    $"Citas: leídas={leidos}, nuevas={importados}, inválidas={invalidos}.");
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private async Task ImportarHistorialMedicoExistenteAsync(
            SqliteConnection connection)
        {
            string ruta = ObtenerRutaArchivo("historial_medico.txt");

            if (!File.Exists(ruta))
            {
                Debug.WriteLine(
                    "No se encontró historial_medico.txt. No hay historial TXT que migrar.");
                return;
            }

            int leidos = 0;
            int importados = 0;
            int invalidos = 0;

            using var transaction = connection.BeginTransaction();

            try
            {
                foreach (string linea in File.ReadLines(ruta))
                {
                    if (string.IsNullOrWhiteSpace(linea))
                        continue;

                    leidos++;

                    string[] p = linea.Split('|');

                    if (p.Length < 11 ||
                        string.IsNullOrWhiteSpace(p[0]) ||
                        string.IsNullOrWhiteSpace(p[1]))
                    {
                        invalidos++;
                        continue;
                    }

                    object peso = DBNull.Value;

                    if (!string.IsNullOrWhiteSpace(p[3]))
                    {
                        if (double.TryParse(
                            p[3].Trim(),
                            NumberStyles.Any,
                            CultureInfo.InvariantCulture,
                            out double valor))
                        {
                            peso = valor;
                        }
                        else
                        {
                            invalidos++;
                            continue;
                        }
                    }

                    string fecha = NormalizarFecha(p[2].Trim());

                    if (string.IsNullOrWhiteSpace(fecha))
                    {
                        invalidos++;
                        continue;
                    }

                    using var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;

                    cmd.CommandText = @"
                        INSERT OR IGNORE INTO HistorialMedico
                        (IdRegistro, IdMascota, Fecha, Peso, Motivo, Sintomas, Diagnostico,
                         Tratamiento, Alergias, Antecedentes, Observaciones)
                        SELECT $id, $idMascota, $fecha, $peso, $motivo, $sintomas, $diagnostico,
                               $tratamiento, $alergias, $antecedentes, $observaciones
                        WHERE EXISTS (
                            SELECT 1 FROM Mascotas WHERE IdMascota = $idMascota
                        );";

                    cmd.Parameters.AddWithValue("$id", p[0].Trim());
                    cmd.Parameters.AddWithValue("$idMascota", p[1].Trim());
                    cmd.Parameters.AddWithValue("$fecha", fecha);
                    cmd.Parameters.AddWithValue("$peso", peso);
                    cmd.Parameters.AddWithValue("$motivo", p[4]);
                    cmd.Parameters.AddWithValue("$sintomas", p[5]);
                    cmd.Parameters.AddWithValue("$diagnostico", p[6]);
                    cmd.Parameters.AddWithValue("$tratamiento", p[7]);
                    cmd.Parameters.AddWithValue("$alergias", p[8]);
                    cmd.Parameters.AddWithValue("$antecedentes", p[9]);
                    cmd.Parameters.AddWithValue("$observaciones", p[10]);

                    int resultado = await cmd.ExecuteNonQueryAsync();

                    importados += resultado;

                    if (resultado == 0)
                    {
                        Debug.WriteLine(
                            "Historial omitido (ID duplicado o mascota inexistente): " + p[0].Trim());
                    }
                }

                transaction.Commit();

                Debug.WriteLine(
                    $"Historial médico: leídos={leidos}, nuevos={importados}, inválidos={invalidos}.");
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private async Task ImportarCartillaMedicaExistenteAsync(
            SqliteConnection connection)
        {
            string ruta = ObtenerRutaArchivo("cartilla_medica.txt");

            if (!File.Exists(ruta))
            {
                Debug.WriteLine(
                    "No se encontró cartilla_medica.txt. No hay cartilla TXT que migrar.");
                return;
            }

            int leidos = 0;
            int importados = 0;
            int invalidos = 0;

            using var transaction = connection.BeginTransaction();

            try
            {
                foreach (string linea in File.ReadLines(ruta))
                {
                    if (string.IsNullOrWhiteSpace(linea))
                        continue;

                    leidos++;

                    string[] p = linea.Split('|');

                    if (p.Length < 7 ||
                        string.IsNullOrWhiteSpace(p[0]) ||
                        string.IsNullOrWhiteSpace(p[1]))
                    {
                        invalidos++;
                        continue;
                    }

                    // Normaliza las fechas para que SQLite pueda ordenarlas
                    // y compararlas correctamente.
                    // Si el formato no se reconoce, conserva el texto original.
                    string fechaAplicacion = NormalizarFechaConservandoOriginal(
                        p[4].Trim());

                    string proximaFecha = NormalizarFechaConservandoOriginal(
                        p[5].Trim());

                    using var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;

                    cmd.CommandText = @"
                        INSERT OR IGNORE INTO CartillaMedica
                        (IdRegistro, IdMascota, Tipo, Producto, FechaAplicacion,
                         ProximaFecha, Observaciones)
                        SELECT $id, $idMascota, $tipo, $producto, $fechaAplicacion,
                               $proximaFecha, $observaciones
                        WHERE EXISTS (
                            SELECT 1 FROM Mascotas WHERE IdMascota = $idMascota
                        );";

                    cmd.Parameters.AddWithValue("$id", p[0].Trim());
                    cmd.Parameters.AddWithValue("$idMascota", p[1].Trim());
                    cmd.Parameters.AddWithValue("$tipo", p[2]);
                    cmd.Parameters.AddWithValue("$producto", p[3]);
                    cmd.Parameters.AddWithValue("$fechaAplicacion", fechaAplicacion);
                    cmd.Parameters.AddWithValue("$proximaFecha", proximaFecha);
                    cmd.Parameters.AddWithValue("$observaciones", p[6]);

                    int resultado = await cmd.ExecuteNonQueryAsync();

                    importados += resultado;

                    if (resultado == 0)
                    {
                        Debug.WriteLine(
                            "Cartilla omitida (ID duplicado o mascota inexistente): " + p[0].Trim());
                    }
                }

                transaction.Commit();

                Debug.WriteLine(
                    $"Cartilla médica: leídos={leidos}, nuevos={importados}, inválidos={invalidos}.");
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private static string NormalizarFechaConservandoOriginal(string fecha)
        {
            if (string.IsNullOrWhiteSpace(fecha))
                return "";

            string fechaNormalizada = NormalizarFecha(fecha);

            return string.IsNullOrWhiteSpace(fechaNormalizada)
                ? fecha
                : fechaNormalizada;
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
                out DateTime fechaConvertida))
            {
                return fechaConvertida.ToString(
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture);
            }

            if (DateTime.TryParse(
                fecha,
                CultureInfo.GetCultureInfo("es-MX"),
                DateTimeStyles.None,
                out fechaConvertida))
            {
                return fechaConvertida.ToString(
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture);
            }

            return "";
        }

        public async Task<string> ObtenerContextoVeterinarioAsync(
            string? idMascota)
        {
            using var connection = await OpenConnectionAsync();

            var contexto = new System.Text.StringBuilder();

            contexto.AppendLine(
                "DATOS CONSULTADOS DIRECTAMENTE EN LA BASE SQLITE DE LA VETERINARIA.");

            contexto.AppendLine(
                "Usa únicamente los datos que aparecen a continuación. Si un campo o registro no aparece, indica que no está registrado.");

            contexto.AppendLine();

            if (string.IsNullOrWhiteSpace(idMascota))
            {
                contexto.AppendLine("No hay una mascota seleccionada.");

                using var cmd = connection.CreateCommand();

                cmd.CommandText = @"
                    SELECT m.IdMascota, m.Nombre, m.Especie, m.Raza, c.Nombre
                    FROM Mascotas m
                    LEFT JOIN Clientes c ON c.IdCliente = m.IdCliente
                    ORDER BY m.Nombre
                    LIMIT 30;";

                using var reader = await cmd.ExecuteReaderAsync();

                contexto.AppendLine("LISTA DE MASCOTAS REGISTRADAS (máximo 30):");

                int cantidad = 0;

                while (await reader.ReadAsync())
                {
                    cantidad++;

                    contexto.AppendLine(
                        $"- ID: {reader.GetString(0)} | Nombre: {reader.GetString(1)} | Especie: {Valor(reader, 2)} | Raza: {Valor(reader, 3)} | Propietario: {Valor(reader, 4)}");
                }

                if (cantidad == 0)
                    contexto.AppendLine("No hay mascotas registradas en SQLite.");

                contexto.AppendLine(
                    "Para consultar citas, historial o cartilla de una mascota, solicita que se seleccione primero.");

                return contexto.ToString();
            }

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT m.IdMascota, m.Nombre, m.Especie, m.Raza, m.Edad, m.Sexo,
                           m.Peso, c.IdCliente, c.Nombre, c.Telefono, c.Correo
                    FROM Mascotas m
                    LEFT JOIN Clientes c ON c.IdCliente = m.IdCliente
                    WHERE m.IdMascota = $id
                    LIMIT 1;";

                cmd.Parameters.AddWithValue("$id", idMascota);

                using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    contexto.AppendLine(
                        $"No se encontró en SQLite la mascota con ID {idMascota}.");

                    return contexto.ToString();
                }

                contexto.AppendLine("=== MASCOTA Y PROPIETARIO ===");
                contexto.AppendLine($"ID mascota: {reader.GetString(0)}");
                contexto.AppendLine($"Nombre: {reader.GetString(1)}");
                contexto.AppendLine($"Especie: {Valor(reader, 2)}");
                contexto.AppendLine($"Raza: {Valor(reader, 3)}");
                contexto.AppendLine($"Edad registrada: {Valor(reader, 4)}");
                contexto.AppendLine($"Sexo: {Valor(reader, 5)}");
                contexto.AppendLine($"Peso actual registrado: {Valor(reader, 6)} kg");
                contexto.AppendLine($"ID propietario: {Valor(reader, 7)}");
                contexto.AppendLine($"Nombre propietario: {Valor(reader, 8)}");
                contexto.AppendLine($"Teléfono propietario: {Valor(reader, 9)}");
                contexto.AppendLine($"Correo propietario: {Valor(reader, 10)}");
            }

            contexto.AppendLine();
            contexto.AppendLine("=== CITAS REGISTRADAS (hasta 15 más recientes) ===");

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT Fecha, Hora, Servicio, Observaciones
                    FROM Citas
                    WHERE IdMascota = $id
                    ORDER BY Fecha DESC, Hora DESC
                    LIMIT 15;";

                cmd.Parameters.AddWithValue("$id", idMascota);

                using var reader = await cmd.ExecuteReaderAsync();

                int n = 0;

                while (await reader.ReadAsync())
                {
                    n++;

                    contexto.AppendLine(
                        $"Cita {n}: Fecha {Valor(reader, 0)}; Hora {Valor(reader, 1)}; Servicio {Valor(reader, 2)}; Observaciones {Valor(reader, 3)}.");
                }

                if (n == 0)
                {
                    contexto.AppendLine(
                        "No hay citas registradas en SQLite para esta mascota.");
                }
            }

            contexto.AppendLine();
            contexto.AppendLine("=== HISTORIAL MÉDICO (hasta 15 registros más recientes) ===");

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT Fecha, Peso, Motivo, Sintomas, Diagnostico, Tratamiento,
                           Alergias, Antecedentes, Observaciones
                    FROM HistorialMedico
                    WHERE IdMascota = $id
                    ORDER BY Fecha DESC
                    LIMIT 15;";

                cmd.Parameters.AddWithValue("$id", idMascota);

                using var reader = await cmd.ExecuteReaderAsync();

                int n = 0;

                while (await reader.ReadAsync())
                {
                    n++;

                    contexto.AppendLine(
                        $"Registro médico {n}: Fecha {Valor(reader, 0)}; Peso {Valor(reader, 1)} kg; Motivo {Valor(reader, 2)}; Síntomas {Valor(reader, 3)}; Diagnóstico documentado {Valor(reader, 4)}; Tratamiento registrado {Valor(reader, 5)}; Alergias {Valor(reader, 6)}; Antecedentes {Valor(reader, 7)}; Observaciones {Valor(reader, 8)}.");
                }

                if (n == 0)
                {
                    contexto.AppendLine(
                        "No hay historial médico registrado en SQLite para esta mascota.");
                }
            }

            contexto.AppendLine();
            contexto.AppendLine("=== CARTILLA MÉDICA (hasta 20 registros) ===");

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT Tipo, Producto, FechaAplicacion, ProximaFecha, Observaciones
                    FROM CartillaMedica
                    WHERE IdMascota = $id
                    ORDER BY FechaAplicacion DESC
                    LIMIT 20;";

                cmd.Parameters.AddWithValue("$id", idMascota);

                using var reader = await cmd.ExecuteReaderAsync();

                int n = 0;

                while (await reader.ReadAsync())
                {
                    n++;

                    contexto.AppendLine(
                        $"Registro cartilla {n}: Tipo {Valor(reader, 0)}; Producto {Valor(reader, 1)}; Fecha aplicación {Valor(reader, 2)}; Próxima fecha {Valor(reader, 3)}; Observaciones {Valor(reader, 4)}.");
                }

                if (n == 0)
                {
                    contexto.AppendLine(
                        "No hay registros en la cartilla médica de SQLite para esta mascota.");
                }
            }

            contexto.AppendLine();
            contexto.AppendLine(
                "REGLA: no confundas ausencia de registro con ausencia de enfermedad, alergia, vacuna o tratamiento. No completes datos faltantes por deducción.");

            return contexto.ToString();
        }

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
