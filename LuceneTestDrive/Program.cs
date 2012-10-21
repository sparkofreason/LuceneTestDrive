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
using System.Text.RegularExpressions;
using System.Net;
using Similarity.Net;
using System.Collections;

namespace LuceneTestDrive
{
    class Program
    {
        static Repository Repo { get; set; }
        static string[] StopWords = new[] {  
                    "I", "A", "Be", "The", "An", "And", "Or", "But", "This", "is", "should",
                    "to", "from", "for", "reasonable", "access" };
        static void Main(string[] args)
        {
            var stopWords = new Hashtable(StringComparer.InvariantCultureIgnoreCase);
            foreach (var s in StopWords)
            {
                stopWords[s] = s;
            }
            var connectionString = "Data Source=(local);Initial Catalog=AllgressDB;Integrated Security=true";
            Repo = new Repository(connectionString);
            var directory = PopulateIndex();

            //SearchForTerm(directory);
            var policy = Repo.GetPolicySection(35);
            string text = Convert(policy.Text);
            using (var reader = DirectoryReader.Open(directory, true))
            using (var indexSearcher = new IndexSearcher(reader))
            {
                var moreLikeThis = new MoreLikeThis(reader);
                moreLikeThis.SetStopWords(stopWords);
                moreLikeThis.SetFieldNames(new[] { "description", "procedures", "objectives", "references" });
                moreLikeThis.SetBoost(true);
                moreLikeThis.SetMinDocFreq(2);
                moreLikeThis.SetMinTermFreq(1);
                var query = moreLikeThis.Like(text);
                (query as BooleanQuery).Add(new TermQuery(new Term("type", "policy")), Occur.MUST_NOT);
                moreLikeThis.SetAnalyzer(new StandardAnalyzer(Lucene.Net.Util.Version.LUCENE_29));
                var docs = from scoreDoc in indexSearcher.Search(query, null, 1000).ScoreDocs
                           select new ScoredDocument { Score = scoreDoc.Score, Document = indexSearcher.Doc(scoreDoc.Doc) };
                var results = docs.ToArray();
            }
            Console.ReadKey();
        }

        private static void SearchForTerm(Lucene.Net.Store.Directory directory)
        {
            var results = Search(directory, "text:email~");
            foreach (var doc in results.Take(5))
            {
                Console.WriteLine("Score = {0}", doc.Score);
                Console.WriteLine("Text\n{0}", doc.Document.Get("text"));
                Console.WriteLine("-----------------------------------------");
            }
        }

        static Lucene.Net.Store.Directory PopulateIndex()
        {
            var policies = Repo.GetAllPolicySections();
            var standards = Repo.GetAllStandardComponents();

            var analyzer = new StandardAnalyzer(Lucene.Net.Util.Version.LUCENE_29);

            // Store the index in memory:
            var directory = new RAMDirectory();
            // To store an index on disk, use this instead:
            //Directory directory = FSDirectory.open("/tmp/testindex");
            using (var indexWriter = new IndexWriter(directory, analyzer, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                foreach (var policy in policies)
                {
                    Document doc = new Document();
                    doc.Add(new Field("id", policy.Id.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED));
                    doc.Add(new Field("type", "policy", Field.Store.YES, Field.Index.NOT_ANALYZED));
                    doc.Add(new Field("sectionName", policy.SectionName, Field.Store.YES, Field.Index.NOT_ANALYZED));
                    doc.Add(new Field("policyName", policy.PolicyName, Field.Store.YES, Field.Index.NOT_ANALYZED));
                    if (policy.Comments != null)
                    {
                        doc.Add(new Field("comments", Convert(policy.Comments), Field.Store.YES, Field.Index.ANALYZED));
                    }
                    if (policy.Text != null)
                    {
                        doc.Add(new Field("text", Convert(policy.Text), Field.Store.YES, Field.Index.ANALYZED));
                    }
                    indexWriter.AddDocument(doc);
                }

                foreach (var standard in standards)
                {
                    Document doc = new Document();
                    doc.Add(new Field("id", standard.Id.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED));
                    doc.Add(new Field("type", "standard", Field.Store.YES, Field.Index.NOT_ANALYZED));
                    doc.Add(new Field("standardname", standard.StandardName, Field.Store.YES, Field.Index.NOT_ANALYZED));
                    doc.Add(new Field("number", standard.Number, Field.Store.YES, Field.Index.NOT_ANALYZED));
                    doc.Add(new Field("name", standard.Name, Field.Store.YES, Field.Index.NOT_ANALYZED));
                    if (standard.Description != null)
                    {
                        doc.Add(new Field("description", Convert(standard.Description), Field.Store.YES, Field.Index.ANALYZED));
                    }
                    if (standard.Procedures != null)
                    {
                        doc.Add(new Field("procedures", Convert(standard.Procedures), Field.Store.YES, Field.Index.ANALYZED));
                    }
                    if (standard.Objectives != null)
                    {
                        doc.Add(new Field("objectives", Convert(standard.Objectives), Field.Store.YES, Field.Index.ANALYZED));
                    }
                    if (standard.References != null)
                    {
                        doc.Add(new Field("references", Convert(standard.References), Field.Store.YES, Field.Index.ANALYZED));
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
                           select new ScoredDocument { Score = scoreDoc.Score, Document = indexSearcher.Doc(scoreDoc.Doc) };
                return docs.ToArray();
            }
        }

        static string Convert(string template)
        {
            template = Regex.Replace(template, "<img .*?alt=[\"']?([^\"']*)[\"']?.*?/?>", "$1"); // Use image alt text. 
            template = Regex.Replace(template, "<a .*?href=[\"']?([^\"']*)[\"']?.*?>(.*)</a>", "$2 [$1]"); // Convert links to something useful 
            template = Regex.Replace(template, "<(/p|/div|/h\\d|br)\\w?/?>", " "); // Let's try to keep vertical whitespace intact. 
            template = Regex.Replace(template, "<[A-Za-z/][^<>]*>", ""); // Remove the rest of the tags. 
            template = Regex.Replace(template, "\n", " "); // Remove the rest of the tags. 
            template = Regex.Replace(template, "\r", " "); // Remove the rest of the tags. 
            template = Regex.Replace(template, "<", " "); // Remove the rest of the tags. 
            template = WebUtility.HtmlDecode(template); // Convert to plain text

            return template;
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
            var db = Database.OpenConnection(ConnectionString);
            return db.StandardComponent.All().Select(
                db.StandardComponent.Id,
                db.StandardComponent.Standard.Name.As("StandardName"),
                db.StandardComponent.Number,
                db.StandardComponent.Name,
                db.StandardComponent.Description,
                db.StandardComponent.Procedures,
                db.StandardComponent.Objectives,
                db.StandardComponent.References
                );
        }

        public dynamic GetPolicySection(int id)
        {
            var db = Database.OpenConnection(ConnectionString);
            return db.PolicySectionText.FindById(id);
        }
    }
}
