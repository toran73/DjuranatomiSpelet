// Djuranatomi Spelet
// Copyright (c) 2026 Torbjörn Nilsson och DSS Maja Blomqvist
// SPDX-License-Identifier: EUPL-1.2

using System.IO;
using DjuranatomiSpelet.Core;
using Microsoft.Data.Sqlite;

namespace DjuranatomiSpelet.Data;

/// <summary>
/// Everything is stored in one SQLite file (djuranatomi.db), images included, so the file can be copied
/// to another computer or shared between teachers.
///
///   Animals   (Id, Name, Image BLOB, SpaceSize)
///   BodyParts (Id, AnimalId, Name, Color, SortOrder)
///   Spaces    (AnimalId, Seq, X, Y, BodyPartId)     X/Y relative to the image (0..1)
///   Questions (Id, BodyPartId, Difficulty, Text, Answer)
/// </summary>
public sealed class SqliteGameRepository : IGameRepository
{
    public SqliteGameRepository(string dbPath)
    {
        DbPath = dbPath;
        EnsureCreated();
    }

    public string DbPath { get; }

    /// <summary>djuranatomi.db next to the program if that folder is writable, otherwise in %LocalAppData%\DjuranatomiSpelet.</summary>
    public static string DefaultPath()
    {
        var local = Path.Combine(AppContext.BaseDirectory, "djuranatomi.db");
        if (File.Exists(local) || IsWritable(AppContext.BaseDirectory))
            return local;

        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DjuranatomiSpelet");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "djuranatomi.db");
    }

    private static bool IsWritable(string dir)
    {
        try
        {
            var probe = Path.Combine(dir, $".write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection($"Data Source={DbPath}");
        connection.Open();
        Exec(connection, null, "PRAGMA foreign_keys = ON;");
        return connection;
    }

    private static void Exec(SqliteConnection c, SqliteTransaction? tx, string sql, params (string Name, object? Value)[] args)
    {
        using var cmd = Command(c, tx, sql, args);
        cmd.ExecuteNonQuery();
    }

    private static SqliteCommand Command(SqliteConnection c, SqliteTransaction? tx, string sql, params (string Name, object? Value)[] args)
    {
        var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        foreach (var (name, value) in args)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return cmd;
    }

    private static long Scalar(SqliteConnection c, SqliteTransaction? tx, string sql, params (string Name, object? Value)[] args)
    {
        using var cmd = Command(c, tx, sql, args);
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    // ------------------------------------------------------------------ schema + example

    private void EnsureCreated()
    {
        using var c = Open();
        Exec(c, null, """
            CREATE TABLE IF NOT EXISTS Animals (
                Id        INTEGER PRIMARY KEY AUTOINCREMENT,
                Name      TEXT NOT NULL,
                Image     BLOB,
                SpaceSize REAL NOT NULL DEFAULT 0.033
            );
            CREATE TABLE IF NOT EXISTS BodyParts (
                Id        INTEGER PRIMARY KEY AUTOINCREMENT,
                AnimalId  INTEGER NOT NULL REFERENCES Animals(Id) ON DELETE CASCADE,
                Name      TEXT NOT NULL,
                Color     TEXT NOT NULL,
                SortOrder INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS Spaces (
                AnimalId   INTEGER NOT NULL REFERENCES Animals(Id) ON DELETE CASCADE,
                Seq        INTEGER NOT NULL,
                X          REAL NOT NULL,
                Y          REAL NOT NULL,
                BodyPartId INTEGER REFERENCES BodyParts(Id) ON DELETE CASCADE,
                PRIMARY KEY (AnimalId, Seq)
            );
            CREATE TABLE IF NOT EXISTS Questions (
                Id         INTEGER PRIMARY KEY AUTOINCREMENT,
                BodyPartId INTEGER NOT NULL REFERENCES BodyParts(Id) ON DELETE CASCADE,
                Difficulty TEXT NOT NULL,
                Text       TEXT NOT NULL,
                Answer     TEXT NOT NULL
            );
            """);

        MigrateSpacesAllowNullPart(c);

        if (Scalar(c, null, "SELECT COUNT(*) FROM Animals;") == 0)
            AddExampleAnimal();
    }

    /// <summary>Older databases have BodyPartId NOT NULL on Spaces. Empty spaces need NULL, so rebuild the table once.</summary>
    private void MigrateSpacesAllowNullPart(SqliteConnection c)
    {
        bool notNull;
        using (var cmd = Command(c, null, "SELECT \"notnull\" FROM pragma_table_info('Spaces') WHERE name = 'BodyPartId';"))
            notNull = Convert.ToInt32(cmd.ExecuteScalar() ?? 0) == 1;
        if (!notNull) return;

        using var tx = c.BeginTransaction();
        Exec(c, tx, """
            CREATE TABLE Spaces_new (
                AnimalId   INTEGER NOT NULL REFERENCES Animals(Id) ON DELETE CASCADE,
                Seq        INTEGER NOT NULL,
                X          REAL NOT NULL,
                Y          REAL NOT NULL,
                BodyPartId INTEGER REFERENCES BodyParts(Id) ON DELETE CASCADE,
                PRIMARY KEY (AnimalId, Seq)
            );
            INSERT INTO Spaces_new (AnimalId, Seq, X, Y, BodyPartId) SELECT AnimalId, Seq, X, Y, BodyPartId FROM Spaces;
            DROP TABLE Spaces;
            ALTER TABLE Spaces_new RENAME TO Spaces;
            """);
        tx.Commit();
    }

    /// <summary>Adds the built-in example dog with its questions. Can also be called to restore it.</summary>
    public int AddExampleAnimal()
    {
        var (animal, questions) = ExampleData.CreateDog();
        var id = SaveAnimal(animal);
        var byName = animal.Parts.ToDictionary(p => p.Name, p => p.Id);
        SaveQuestions(questions.Select(x =>
        {
            x.Question.BodyPartId = byName[x.PartName];
            return x.Question;
        }), Array.Empty<int>());
        return id;
    }

    // ------------------------------------------------------------------ animals

    public IReadOnlyList<AnimalInfo> ListAnimals()
    {
        using var c = Open();
        using var cmd = Command(c, null, """
            SELECT a.Id, a.Name,
                   (SELECT COUNT(*) FROM Spaces s WHERE s.AnimalId = a.Id),
                   (SELECT COUNT(*) FROM Questions q JOIN BodyParts b ON b.Id = q.BodyPartId WHERE b.AnimalId = a.Id)
            FROM Animals a
            ORDER BY a.Name COLLATE NOCASE;
            """);
        using var r = cmd.ExecuteReader();
        var list = new List<AnimalInfo>();
        while (r.Read())
            list.Add(new AnimalInfo(r.GetInt32(0), r.GetString(1), r.GetInt32(2), r.GetInt32(3)));
        return list;
    }

    public Animal LoadAnimal(int id)
    {
        using var c = Open();
        Animal animal;
        using (var cmd = Command(c, null, "SELECT Id, Name, Image, SpaceSize FROM Animals WHERE Id = $id;", ("$id", id)))
        using (var r = cmd.ExecuteReader())
        {
            if (!r.Read())
                throw new InvalidOperationException($"Djuret med id {id} finns inte.");
            animal = new Animal
            {
                Id = r.GetInt32(0),
                Name = r.GetString(1),
                Image = r.IsDBNull(2) ? null : r.GetFieldValue<byte[]>(2),
                SpaceSize = r.GetDouble(3)
            };
        }

        using (var cmd = Command(c, null, "SELECT Id, Name, Color FROM BodyParts WHERE AnimalId = $id ORDER BY SortOrder, Id;", ("$id", id)))
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
                animal.Parts.Add(new BodyPart { Id = r.GetInt32(0), Name = r.GetString(1), Color = r.GetString(2) });
        }

        var partsById = animal.Parts.ToDictionary(p => p.Id);
        using (var cmd = Command(c, null, "SELECT X, Y, BodyPartId FROM Spaces WHERE AnimalId = $id ORDER BY Seq;", ("$id", id)))
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                // NULL BodyPartId = empty space (no question).
                BodyPart? part = null;
                if (!r.IsDBNull(2) && !partsById.TryGetValue(r.GetInt32(2), out part))
                    continue;
                animal.Spaces.Add(new BoardSpace { X = r.GetDouble(0), Y = r.GetDouble(1), Part = part });
            }
        }
        return animal;
    }

    public int SaveAnimal(Animal animal)
    {
        using var c = Open();
        using var tx = c.BeginTransaction();

        if (animal.Id == 0)
        {
            animal.Id = (int)Scalar(c, tx,
                "INSERT INTO Animals (Name, Image, SpaceSize) VALUES ($name, $img, $size); SELECT last_insert_rowid();",
                ("$name", animal.Name.Trim()), ("$img", animal.Image), ("$size", animal.SpaceSize));
        }
        else
        {
            Exec(c, tx, "UPDATE Animals SET Name = $name, Image = $img, SpaceSize = $size WHERE Id = $id;",
                ("$name", animal.Name.Trim()), ("$img", animal.Image), ("$size", animal.SpaceSize), ("$id", animal.Id));
        }

        // Spaces are rewritten every time (they are just an ordered list).
        Exec(c, tx, "DELETE FROM Spaces WHERE AnimalId = $id;", ("$id", animal.Id));

        // Body parts: insert new, update existing, delete removed (their questions go with them).
        for (int i = 0; i < animal.Parts.Count; i++)
        {
            var p = animal.Parts[i];
            if (p.Id == 0)
            {
                p.Id = (int)Scalar(c, tx,
                    "INSERT INTO BodyParts (AnimalId, Name, Color, SortOrder) VALUES ($a, $name, $color, $sort); SELECT last_insert_rowid();",
                    ("$a", animal.Id), ("$name", p.Name.Trim()), ("$color", p.Color), ("$sort", i));
            }
            else
            {
                Exec(c, tx, "UPDATE BodyParts SET Name = $name, Color = $color, SortOrder = $sort WHERE Id = $id AND AnimalId = $a;",
                    ("$name", p.Name.Trim()), ("$color", p.Color), ("$sort", i), ("$id", p.Id), ("$a", animal.Id));
            }
        }

        var keep = animal.Parts.Select(p => p.Id).ToHashSet();
        var existing = new List<int>();
        using (var cmd = Command(c, tx, "SELECT Id FROM BodyParts WHERE AnimalId = $a;", ("$a", animal.Id)))
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read()) existing.Add(r.GetInt32(0));
        }
        foreach (var removed in existing.Where(id => !keep.Contains(id)))
            Exec(c, tx, "DELETE FROM BodyParts WHERE Id = $id;", ("$id", removed));

        for (int i = 0; i < animal.Spaces.Count; i++)
        {
            var s = animal.Spaces[i];
            Exec(c, tx, "INSERT INTO Spaces (AnimalId, Seq, X, Y, BodyPartId) VALUES ($a, $seq, $x, $y, $p);",
                ("$a", animal.Id), ("$seq", i), ("$x", s.X), ("$y", s.Y), ("$p", s.Part?.Id));
        }

        tx.Commit();
        return animal.Id;
    }

    public void DeleteAnimal(int id)
    {
        using var c = Open();
        Exec(c, null, "DELETE FROM Animals WHERE Id = $id;", ("$id", id));
    }

    /// <summary>Number of questions per body part id (used to warn before deleting a part).</summary>
    public Dictionary<int, int> QuestionCountsByPart(int animalId)
    {
        using var c = Open();
        using var cmd = Command(c, null, """
            SELECT q.BodyPartId, COUNT(*) FROM Questions q JOIN BodyParts b ON b.Id = q.BodyPartId
            WHERE b.AnimalId = $a GROUP BY q.BodyPartId;
            """, ("$a", animalId));
        using var r = cmd.ExecuteReader();
        var result = new Dictionary<int, int>();
        while (r.Read()) result[r.GetInt32(0)] = r.GetInt32(1);
        return result;
    }

    // ------------------------------------------------------------------ questions

    public IReadOnlyList<Question> GetQuestions(int animalId)
    {
        using var c = Open();
        using var cmd = Command(c, null, """
            SELECT q.Id, q.BodyPartId, q.Difficulty, q.Text, q.Answer
            FROM Questions q JOIN BodyParts b ON b.Id = q.BodyPartId
            WHERE b.AnimalId = $a
            ORDER BY b.SortOrder, q.Difficulty, q.Id;
            """, ("$a", animalId));
        using var r = cmd.ExecuteReader();
        var list = new List<Question>();
        while (r.Read())
        {
            DifficultyInfo.TryParse(r.GetString(2), out var difficulty);
            list.Add(new Question
            {
                Id = r.GetInt32(0),
                BodyPartId = r.GetInt32(1),
                Difficulty = difficulty,
                Text = r.GetString(3),
                Answer = r.GetString(4)
            });
        }
        // Sort by difficulty level, not by the stored text.
        return list.OrderBy(q => q.BodyPartId).ThenBy(q => q.Difficulty).ThenBy(q => q.Id).ToList();
    }

    public void SaveQuestions(IEnumerable<Question> questions, IEnumerable<int> deletedIds)
    {
        using var c = Open();
        using var tx = c.BeginTransaction();

        foreach (var id in deletedIds.Where(id => id > 0))
            Exec(c, tx, "DELETE FROM Questions WHERE Id = $id;", ("$id", id));

        foreach (var q in questions)
        {
            if (string.IsNullOrWhiteSpace(q.Text) || q.BodyPartId == 0) continue;
            var args = new (string, object?)[]
            {
                ("$p", q.BodyPartId), ("$d", q.Difficulty.ToString()), ("$t", q.Text.Trim()), ("$s", (q.Answer ?? "").Trim())
            };
            if (q.Id == 0)
                q.Id = (int)Scalar(c, tx, "INSERT INTO Questions (BodyPartId, Difficulty, Text, Answer) VALUES ($p, $d, $t, $s); SELECT last_insert_rowid();", args);
            else
                Exec(c, tx, "UPDATE Questions SET BodyPartId = $p, Difficulty = $d, Text = $t, Answer = $s WHERE Id = $id;",
                    args.Append(("$id", (object?)q.Id)).ToArray());
        }

        tx.Commit();
    }
}
