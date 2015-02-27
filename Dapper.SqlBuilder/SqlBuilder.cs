using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Dapper
{
    public class SqlBuilder
    {
        private readonly Dictionary<string, Clauses> data = new Dictionary<string, Clauses>();
        private int sequance;

        private class Clause
        {
            public string Sql { get; set; }
            public object Parameters { get; set; }
            public bool IsInclusive { get; set; }
        }

        private class Clauses : List<Clause>
        {
            private readonly string joiner;
            private readonly string prefix;
            private readonly string postfix;

            public Clauses(string joiner, string prefix = "", string postfix = "")
            {
                this.joiner = joiner;
                this.prefix = prefix;
                this.postfix = postfix;
            }

            public string ResolveClauses(DynamicParameters parameters)
            {
                foreach (var clause in this)
                {
                    parameters.AddDynamicParams(clause.Parameters);
                }

                return this.Any(a => a.IsInclusive)
                    ? prefix +
                      string.Join(joiner,
                          this.Where(a => !a.IsInclusive)
                              .Select(c => c.Sql)
                              .Union(new []
                              {
                                  " ( " +
                                  string.Join(" OR ", this.Where(a => a.IsInclusive).Select(c => c.Sql).ToArray()) +
                                  " ) "
                              })) + postfix
                    : prefix + string.Join(joiner, this.Select(c => c.Sql)) + postfix;
            }
        }

        public class Template
        {
            private readonly string sql;
            private readonly SqlBuilder builder;
            private readonly object initialParameters;
            private int sequance = -1; // Unresolved
            private static readonly Regex Regex = new Regex(@"\/\*\*.+\*\*\/", RegexOptions.Compiled | RegexOptions.Multiline);
            private string rawSql;
            private object parameters;

            public Template(SqlBuilder builder, string sql, dynamic parameters)
            {
                this.initialParameters = parameters;
                this.sql = sql;
                this.builder = builder;
            }

            public string RawSql
            {
                get
                {
                    ResolveSql(); 

                    return rawSql;
                }
            }

            public object Parameters
            {
                get
                {
                    ResolveSql(); 

                    return parameters;
                }
            }

            private void ResolveSql()
            {
                if (sequance != builder.sequance)
                {
                    var p = new DynamicParameters(initialParameters);

                    rawSql = sql;

                    foreach (var pair in builder.data)
                    {
                        rawSql = rawSql.Replace("/**" + pair.Key + "**/", pair.Value.ResolveClauses(p));
                    }

                    parameters = p;

                    // replace all that is left with empty
                    rawSql = Regex.Replace(rawSql, "");

                    sequance = builder.sequance;
                }
            }
        }

        public Template AddTemplate(string sql, dynamic parameters = null)
        {
            return new Template(this, sql, parameters);
        }

        public SqlBuilder AddClause(string name, string sql, dynamic parameters = null, string joiner = "", string prefix = "", string postfix = "", bool inclusive = false)
        {
            Clauses clauses;
            if (!data.TryGetValue(name, out clauses))
            {
                clauses = new Clauses(joiner, prefix, postfix);
                data[name] = clauses;
            }
            clauses.Add(new Clause { Sql = sql, Parameters = parameters, IsInclusive = inclusive });
            sequance++;
            return this;
        }

        public SqlBuilder Intersect(string sql, dynamic parameters = null)
        {
            return AddClause("intersect", sql, parameters, joiner: "\nINTERSECT\n ", prefix: "\n ", postfix: "\n");
        }
        
        public SqlBuilder InnerJoin(string sql, dynamic parameters = null)
        {
            return AddClause("innerjoin", sql, parameters, joiner: "\nINNER JOIN ", prefix: "\nINNER JOIN ", postfix: "\n");
        }

        public SqlBuilder LeftJoin(string sql, dynamic parameters = null)
        {
            return AddClause("leftjoin", sql, parameters, joiner: "\nLEFT JOIN ", prefix: "\nLEFT JOIN ", postfix: "\n");
        }

        public SqlBuilder RightJoin(string sql, dynamic parameters = null)
        {
            return AddClause("rightjoin", sql, parameters, joiner: "\nRIGHT JOIN ", prefix: "\nRIGHT JOIN ", postfix: "\n");
        }

        public SqlBuilder Where(string sql, dynamic parameters = null)
        {
            return AddClause("where", sql, parameters, " AND ", prefix: "WHERE ", postfix: "\n");
        }

        public SqlBuilder OrWhere(string sql, dynamic parameters = null)
        {
            return AddClause("where", sql, parameters, " AND ", prefix: "WHERE ", postfix: "\n", inclusive: true);
        }
        
        public SqlBuilder OrderBy(string sql, dynamic parameters = null)
        {
            return AddClause("orderby", sql, parameters, ", ", prefix: "ORDER BY ", postfix: "\n");
        }

        public SqlBuilder Select(string sql, dynamic parameters = null)
        {
            return AddClause("select", sql, parameters, ", ", prefix: "", postfix: "\n");
        }

        public SqlBuilder AddParameters(dynamic parameters)
        {
            return AddClause("--parameters", "", parameters, "");
        }

        public SqlBuilder Join(string sql, dynamic parameters = null)
        {
            return AddClause("join", sql, parameters, joiner: "\nJOIN ", prefix: "\nJOIN ", postfix: "\n");
        }

        public SqlBuilder GroupBy(string sql, dynamic parameters = null)
        {
            return AddClause("groupby", sql, parameters, joiner: " , ", prefix: "\nGROUP BY ", postfix: "\n");
        }

        public SqlBuilder Having(string sql, dynamic parameters = null)
        {
            return AddClause("having", sql, parameters, joiner: "\nAND ", prefix: "HAVING ", postfix: "\n");
        }
    }
}
