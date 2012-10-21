using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Simple.Data;
using System.IO;
using Lucene.Net.Documents;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Store;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.QueryParsers;

namespace LuceneTestDrive
{
    class Program
    {
        static void Main(string[] args)
        {
            var directory = PopulateIndex();
            var results = Search(directory, "text:lorem");
            //results.First().doc.g 
        }

        static Lucene.Net.Store.Directory PopulateIndex()
        {
            var connectionString = "Data Source=(local);Initial Catalog=AllgressDB;Integrated Security=true";
            var repo = new Repository(connectionString);
            var policies = repo.GetAllPolicySections();
            var standards = repo.GetAllStandardComponents();

            var analyzer = new StandardAnalyzer(Lucene.Net.Util.Version.LUCENE_29);

            // Store the index in memory:
            var directory = new RAMDirectory();
            // To store an index on disk, use this instead:
            //Directory directory = FSDirectory.open("/tmp/testindex");
            using (var indexWriter = new IndexWriter(directory, analyzer, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                foreach (var standard in policies)
                {
                    Document doc = new Document();
                    doc.Add(new Field("id", standard.Id.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED));
                    doc.Add(new Field("type", "policy", Field.Store.YES, Field.Index.NOT_ANALYZED));
                    doc.Add(new Field("sectionName", standard.SectionName, Field.Store.YES, Field.Index.NOT_ANALYZED));
                    doc.Add(new Field("policyName", standard.PolicyName, Field.Store.YES, Field.Index.NOT_ANALYZED));
                    if (standard.Comments != null)
                    {
                        doc.Add(new Field("comments", standard.Comments, Field.Store.YES, Field.Index.ANALYZED));
                    }
                    if (standard.Text != null)
                    {
                        doc.Add(new Field("text", standard.Text, Field.Store.YES, Field.Index.ANALYZED));
                    }
                    indexWriter.AddDocument(doc);
                }
            }

            return directory;
        }

        static IEnumerable<ScoredDocument> Search(Lucene.Net.Store.Directory directory, string queryString)
        {
            // Now search the index:
            using (var reader = DirectoryReader.Open(directory, true))
            using (var indexSearcher = new IndexSearcher(reader))
            {
                // Parse a simple query that searches for "text":
                var parser = new QueryParser(Lucene.Net.Util.Version.LUCENE_29, queryString, new StandardAnalyzer(Lucene.Net.Util.Version.LUCENE_29));
                var query = parser.Parse(queryString);
                var docs = from scoreDoc in indexSearcher.Search(query, null, 1000).ScoreDocs
                           select new ScoredDocument { Score = scoreDoc.score, Document = indexSearcher.Doc(scoreDoc.doc) };
                return docs.ToArray();
            }
        }
    }

    public class ScoredDocument
    {
        public double Score { get; set; }
        public Document Document { get; set; }
    }

    class Repository
    {
        private string ConnectionString { get; set; }

        public Repository(string connectionString)
        {
            ConnectionString = connectionString;
        }

        public IEnumerable<dynamic> GetAllPolicySections()
        {
            var db = Database.OpenConnection(ConnectionString);
            //return db.PolicySectionText.All();
            return db.PolicySectionText.All().Select(
                db.PolicySectionText.Id,
                db.PolicySectionText.Comments,
                db.PolicySectionText.Text,
                db.PolicySectionText.PolicySection.Name.As("SectionName"),
                db.PolicySectionText.PolicySection.Policy.Name.As("PolicyName")
                );
        }

        public IEnumerable<dynamic> GetAllStandardComponents()
        {
            throw new NotFiniteNumberException();
        }
    }
}
