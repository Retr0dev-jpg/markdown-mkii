using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Xunit.Abstractions;

namespace MarkdownMkII.Storage.Tests;
public sealed class LargeCatalogTests(ITestOutputHelper output)
{
    [Fact]
    public async Task HundredThousandNotesRemainPagedAndSearchable()
    {
        var root = Path.Combine(Path.GetTempPath(), "mkii-scale-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "notes.db");
        try
        {
            var db = new NoteDatabase(path);
            await db.InitializeAsync();
            using (var connection = new SqliteConnection("Data Source=" + path))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    WITH RECURSIVE sample(n) AS (SELECT 1 UNION ALL SELECT n+1 FROM sample WHERE n<100000)
                    INSERT INTO Notes(Id,Title,Markdown,Preview,Created,Modified)
                    SELECT printf('%032d',n),'Nota '||n,'Contenuto caffè '||n,'Contenuto',n,n FROM sample;
                    """;
                command.ExecuteNonQuery();
            }

            var stopwatch = Stopwatch.StartNew();
            var first = await db.QueryAsync(new());
            var last = await db.QueryAsync(new(Offset: 99900));
            var search = await db.QueryAsync(new(Search: "caffe 99999"));
            var snapshot = await db.QueryIdsAsync(new());
            Assert.Equal(100000, snapshot.Count);
            var stablePage = await db.SummariesAsync(snapshot.Skip(99900).Take(100));
            Assert.Equal(last.Items.Select(n => n.Id), stablePage.Select(n => n.Id));
            stopwatch.Stop();
            Assert.Equal(100000, first.Total);
            Assert.Equal(100, first.Items.Count);
            Assert.Equal(100, last.Items.Count);
            Assert.Single(search.Items);
            Assert.Equal("Nota 99999", search.Items[0].Title);
            Assert.Empty(first.Items.Select(n => n.Id).Intersect(last.Items.Select(n => n.Id)));
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"Catalog queries took {stopwatch.Elapsed}");
            output.WriteLine($"100,000 notes: first page + last page + FTS search in {stopwatch.ElapsedMilliseconds} ms.");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }
}
