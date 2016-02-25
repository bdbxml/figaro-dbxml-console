/*************************************************************************************************
* 
* THIS CODE AND INFORMATION ARE PROVIDED AS IS WITHOUT WARRANTY OF ANY
* KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
* IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
* 
*************************************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Figaro.Utilities.Common;
using Figaro.Utilities.Resources;

namespace Figaro.Utilities
{
    public class FigaroContext : IDisposable
    {
        private readonly XmlManager mgr;
        private readonly DbxmlOptions opts;
        private readonly Stack<Container> containers;
        private readonly UpdateContext updateContext;
        private string path;
        private readonly QueryContext queryContext;
        private XmlResults queryResults;
        private XQueryExpression queryExpression;
        private QueryOptions queryOptions;

#if !DS
// ReSharper disable once UnassignedReadonlyField.Compiler
        private readonly FigaroEnv env;
#endif

#if TDS || HA
        private XmlTransaction trans;
#endif

// ReSharper disable UnusedMember.Local
        private FigaroContext() { }
// ReSharper restore UnusedMember.Local

        public FigaroContext(DbxmlOptions options)
        {
            containers = new Stack<Container>();
            opts = options;
            SetWorkingDirectory();
#if TDS || HA
            bool opened = false;
            bool adopt = true;
            env = new FigaroEnv();

            env.OnErr += env_OnErr;
            env.OnMessage += env_OnMessage;

            if (opts.Verbose)
            {
                env.SetVerbose(VerboseOption.WaitsFor, true);
                env.SetVerbose(VerboseOption.Register, true);
                env.SetVerbose(VerboseOption.Recovery, true);
                env.SetVerbose(VerboseOption.Deadlock, true);
                LogConfiguration.SetCategory(LogConfigurationCategory.All, true);
                LogConfiguration.SetLogLevel(LogConfigurationLevel.All, true);
            }
            else
            {
                LogConfiguration.SetCategory(LogConfigurationCategory.All, true);
                LogConfiguration.SetLogLevel(LogConfigurationLevel.Error, true);
            }
            if (!string.IsNullOrEmpty(opts.Password))
            {
                env.SetEncryption(opts.Password, true);
            }
#if TDS || HA
            env.SetLogOptions(EnvLogOptions.AutoRemove, true);
#endif
            env.MessageEventEnabled = true;
            env.ErrEventEnabled = true;
            var cache = new EnvCacheSize(0, opts.CacheSize > 0 ? opts.CacheSize * 1024 * 1024 : 64 * 1024 * 1024);
            env.SetCacheSize(cache, 5);
            env.SetMaxLockers(10000);
            env.SetMaxLocks(10000);
            env.SetMaxLockedObjects(10000);
// ReSharper disable JoinDeclarationAndInitializer
            EnvOpenOptions openOpts;
// ReSharper restore JoinDeclarationAndInitializer
#if TDS || HA
            openOpts = EnvOpenOptions.InitMemoryBufferPool |
                        EnvOpenOptions.InitLock |
                        EnvOpenOptions.UseEnvironment;

#endif
            msg("attempting to join environment {0}...", path);
#if TDS || HA
            if (opts.Transactional)
            {
                openOpts |= EnvOpenOptions.TransactionDefaults;
            }
#endif
            try
            {
                env.Open(path, openOpts);
                opened = true;

#if TDS || HA
                if (opts.Transactional)
                    env.SetEnvironmentTransactionCheckpoint(false, 1, 0);
#endif
            }
            catch (FigaroEnvException fee)
            {
                if (opts.Create)
                {
                    openOpts ^= EnvOpenOptions.UseEnvironment;
                    openOpts |= EnvOpenOptions.Create;

                    // if the create flag is set, run recovery mode - just in case
                    if (opts.Transactional)
                    {
                        openOpts |= EnvOpenOptions.Recover;
                    }
                }
                else
                {
                    Verbose("Error opening environment: {0}", fee.Message);
                    Verbose(
                        "Skipping environment usage. To explicitly use an environment where none exist, specify the create flag at startup.");
                    adopt = false;
#if TDS || HA
                    if (options.Transactional)
                    {
                        options.Transactional = false;
                        Warn("Switching to non-transactional mode.");
                    }
#endif
                    env.Close();
                    env.Dispose(); // get rid of it
                    env = null;
                }
            }
            catch (RunRecoveryException)
            {
#if TDS || HA
                openOpts ^= EnvOpenOptions.Register;
                openOpts |= EnvOpenOptions.Recover;
#endif

                // recover flag requires create to be specified
                if (openOpts.ToString().Contains(EnvOpenOptions.UseEnvironment.ToString()))
                    openOpts ^= EnvOpenOptions.UseEnvironment;
            }
            // try again
#if TDS || HA
            if (openOpts.ToString().Contains(EnvOpenOptions.InitLog.ToString()))
                if (env != null) env.SetLogOptions(EnvLogOptions.AutoRemove, true);
#endif
            if (adopt)
            {
                if (!opened) env.Open(path, openOpts);
                msg("Environment opened successfully.");
            }
#endif
#if TDS || HA
            mgr = adopt ? new XmlManager(env, ManagerInitOptions.AllOptions) :
                new XmlManager(ManagerInitOptions.AllowAutoOpen | ManagerInitOptions.AllowExternalAccess);
#else
            mgr = new XmlManager(ManagerInitOptions.AllowAutoOpen | ManagerInitOptions.AllowExternalAccess);
#endif
            updateContext = mgr.CreateUpdateContext();
            queryContext = mgr.CreateQueryContext(EvaluationType.Eager);
        }

#if TDS || HA

        static void env_OnMessage(object sender, MsgEventArgs e)
        {
            msg("{0}", e.Message);
        }

        void env_OnErr(object sender, ErrEventArgs e)
        {
            Error("{0} {1}", e.Prefix ?? "[FigaroEnv]", e.Message);
        }

#endif

        public void Abort()
        {
#if TDS || HA
            if (env == null || trans == null) return;
            trans.Abort();
            trans.Dispose();
            trans = null;
            msg("Transaction aborted.");
#endif
        }

        public void AddAlias(string alias)
        {
            if (containers.Count < 1)
            {
                Error("You must create and/or open a container first!");
                return;
            }
            containers.Peek().AddAlias(alias);
            msg("alias '{0}' added to container '{1}'.", alias, containers.Peek().Name);
        }

        public void AddAlias(string containerName, string alias)
        {
            var c = (from cont in containers where cont.Name == containerName select cont).ToList();
            if (!c.Any())
            {
                Error("Container {0} not found in the stack.", containerName);
                return;
            }

            c.First().AddAlias(alias);
            msg("alias '{0}' added to container '{1}'.", alias, containerName);
        }

        public void AddIndex(string ns, string nodeName, string index)
        {
            var idx = new IndexingStrategy(index);
#if TDS || HA
            if (env != null && env.Transactional)
            {
                containers.Peek().AddIndex(trans, ns, nodeName, idx, updateContext);
            }
            else
            {
#endif
                containers.Peek().AddIndex(ns, nodeName, idx, updateContext);
#if TDS || HA
            }
#endif

            var sb = new StringBuilder();

            sb.AppendFormat(DbxmlResources.SuccessfulAddIndex,
                idx.Unique ? DbxmlResources.Unique : DbxmlResources.NonUnique,
                idx.NodeType,
                idx.KeyType,
                idx.PathType,
                idx.NodeType,
                string.IsNullOrEmpty(nodeName) ? DbxmlResources.None : nodeName,
                string.IsNullOrEmpty(ns) ? DbxmlResources.None : ns);
            msg(sb.ToString());
        }

        public void OpenContainer(string containerName, bool validate)
        {
            var cfg = new ContainerConfig
                          {
                              AllowValidation = validate
                          };
#if TDS || HA
            if (env != null && env.Transactional)
            {
                containers.Push(mgr.OpenContainer(trans, containerName, cfg));
                return;
            }
#endif
            containers.Push(mgr.OpenContainer(containerName, cfg));
            if (string.IsNullOrEmpty(containerName))
                msg("in-memory container created");

            msg("container {0} opened.", containerName);
            Verbose("You have {0} {1} open.", containers.Count, containers.Count == 1 ? "container" : "containers");
        }

        public void Preload(string containerName)
        {
            Container c = null;
            if (containers.Count > 0)
                c = containers.Pop();

#if TDS
            if (env != null && env.Transactional)
            {
                containers.Push(mgr.OpenContainer(trans, containerName));
                msg("preloaded {0}",containerName);
            }
            else
            {
#endif
                containers.Push(mgr.OpenContainer(containerName));
                msg("preloaded {0}", containerName);
#if TDS
            }
#endif
            if (c != null) containers.Push(c);
        }

        public void Prepare(string query)
        {
            if (queryExpression != null)
            {
                queryExpression.Dispose();
                queryExpression = null;
            }

#if TDS || HA
            if (env != null && env.Transactional)
            {
                queryExpression = mgr.Prepare(trans, query, queryContext);                
            }
            else
            {
#endif
                queryExpression = mgr.Prepare(query, queryContext);
#if TDS || HA
            }
#endif
            Verbose("\r\n{0} expression '{1}' prepared.\r\n", queryExpression.IsUpdateExpression ? "update" : "query",query);
        }

        public void PrintResults(int count, string outputPath)
        {
            if (queryResults == null || queryResults.Count == 0)
            {
                Warn("No results to print.");
                return;
            }

            if (count == 0) count = queryResults.Count;
            if (count < 0) count = int.MaxValue;
            StreamWriter sw = null;
            if (!string.IsNullOrEmpty(outputPath))
                sw = new StreamWriter(outputPath, false);
            try
            {
                msg(string.Empty);
                var j = 0;
                while (queryResults.HasNext() && j < count)
                {
                    var doc = queryResults.NextDocument();
                    msg(doc.ToString());
                    if (sw != null) sw.WriteLine(doc.ToString());
                    msg(string.Empty);
                    j++;
                }

                if (sw != null)
                {
                    sw.Flush();
                    sw.Close();
                    msg("output written to {0}.", outputPath);
                }
                queryResults.Reset();
            }
            catch (XmlValueException)
            {
                int k = 0;
                while (queryResults.HasNext() && k < count)
                {
                    var val = queryResults.NextValue().ToString();
                    msg(val);
                    if (sw != null) sw.WriteLine(val);
                    k++;
                }
                if (sw != null)
                {
                    sw.Flush();
                    sw.Close();
                    msg("output written to {0}", outputPath);
                }
                queryResults.Reset();
            }
        }

        public void PrintNames(int count, string filePath)
        {
            if (queryResults == null)
            {
                Warn("No results to print.");
                return;
            }

            if (count == 0) count = queryResults.Count;
            //encounter the lazy load scenario
            if (count < 0) count = int.MaxValue;
            try
            {
                StreamWriter sw = null;
                if (!string.IsNullOrEmpty(filePath))
                {
                    sw = new StreamWriter(filePath, false);
                }
                var i = 0;
                while (queryResults.HasNext() && i < count)
                {
                    var doc = queryResults.NextDocument();
                    msg(doc.Name);
                    if (sw != null) sw.WriteLine(doc.Name);
                    i++;
                }
                if (sw != null)
                {
                    sw.Flush();
                    sw.Close();
                }
                msg(string.Empty);
            }
            catch (XmlValueException)
            {
                Warn("query result set is of XmlValue type - no names are available.");
            }
        }

        public void CreateContainer(string containerName, string options, bool validate)
        {
            var cfg = new ContainerConfig
                          {
                              AllowValidation = validate
                          };
            if (options.Equals("in") || options.Equals("n"))
                cfg.ContainerType = XmlContainerType.NodeContainer;
            if (options.Equals("d") || options.Equals("id"))
                cfg.ContainerType = XmlContainerType.WholeDocContainer;

            cfg.IndexNodes = options.Equals("id") || options.Equals("in")
                                 ? ConfigurationState.On
                                 : ConfigurationState.UseDefault;
#if TDS || HA
            if (env != null && env.Transactional)
            {
                containers.Push(mgr.CreateContainer(trans, containerName, cfg));
                return;
            }
#endif
            containers.Push(mgr.CreateContainer(containerName, cfg));
            msg("{0} created and opened.", string.IsNullOrEmpty(containerName) ? "in-memory container" : containerName);
            Verbose("You have {0} {1} open.", containers.Count, containers.Count == 1 ? "container" : "containers");
        }

        public void BeginTransaction()
        {
#if TDS || HA
            if (env == null || !env.Transactional)
            {
                Warn(DbxmlResources.TransactionsNotEnabled);
                return;
            }
            if (trans != null)
            {
                Verbose("Committing transaction before beginning new one...");
                trans.Commit(true);
                trans.Dispose();
                trans = null;
            }
            trans = mgr.CreateTransaction();
            Verbose("Transaction created successfully.");
#endif
        }

        public void Close(string container)
        {
            if (containers.Count < 1)
            {
                msg("no containers to close.\r\n");
                return;
            }

            int i = 0;
#if DEBUG
            try
            {
#endif
                if (string.IsNullOrEmpty(container))
                {
                    // close them all
                    while (containers.Count > 0)
                    {
                        i++;
                        var c = containers.Pop();
                        Verbose("closing container {0}...", c.Name);
                        c.Dispose();
                    }
                    msg("closed {0} containers.", i);
                    return;
                }

                var l = new List<Container>();
                while (containers.Count > 0)
                {
                    var c = containers.Pop();
                    if (c.Name.Equals(container))
                    {
                        msg("container {0} closed.",c.Name);
                        c.Dispose();
                        continue;
                    }
                    l.Add(c);
                }
                if (l.Count == 0 && containers.Count == 0) return;
                l.Reverse();
                i = 0;
                foreach (Container cont in l)
                {
                    i++;
                    containers.Push(cont);
                }
                msg("You have {0} containers open.\r\n", i);
                l.Clear();
#if DEBUG
            }
            finally
            {
                GC.Collect(0, GCCollectionMode.Forced);
            }
#endif
        }

        public void Commit()
        {
#if TDS || HA
            if (env != null && !env.Transactional)
            {
                Warn(DbxmlResources.TransactionsNotEnabled);
                return;
            }
            if (trans == null)
            {
                Warn("No transaction exists!");
                return;
            }
            trans.Commit();
            trans.Dispose();
            trans = null;
            Verbose("Transaction committed.");
#endif
        }

        public void CompactContainer(string containerName)
        {
#if TDS || HA
            if (env != null && trans != null)
            {
                mgr.CompactContainer(trans, containerName, updateContext);
                msg("Container compacted: {0}", containerName);
                return;
            }
#endif
            mgr.CompactContainer(containerName, updateContext);
            msg("Container compacted: {0}", containerName);
        }

        public void CQuery(string query)
        {
#if TDS || HA
            if (env != null && env.Transactional)
            {
                queryResults = mgr.Query(trans, query, queryContext);
                msg("{0} objects returned for eager expression '{1}'.", queryResults.Count, query);
                return;
            }
#endif
            if (queryResults != null) queryResults.Dispose();
            queryResults = mgr.Query(query, queryContext);
            msg("{0} objects returned for eager expression '{1}'.", queryResults.Count, query);
        }

        public void ContextQuery(string query)
        {
#if TDS || HA
            if (env != null && env.Transactional)
            {
                using (var exp = mgr.Prepare(trans, query, queryContext))
                {
                    Verbose("query: \r\n{0}\r\nquery plan: \r\n{1}\r\n", exp.Query, exp.QueryPlan);

                    queryResults.Reset();
                    var tmpRes = mgr.CreateXmlResults();
                    int j = 0;
                    while (queryResults.HasNext())
                    {
                        var xv = queryResults.NextValue();
                        using (var val = exp.Execute(trans, xv, queryContext, queryOptions))
                        {
                            while (val.HasNext())
                            {
                                tmpRes.Add(val.NextValue());
                                j++;
                            }
                        }
                    }
                    msg("query returned {0} results.", j);
                    // last known results, even if nothing returned
                    queryResults.Dispose();
                    queryResults = tmpRes;
                }
                return;
            }
#endif

            using (var exp = mgr.Prepare(query, queryContext))
            {
                Verbose("query: \r\n{0}\r\nquery plan: \r\n{1}\r\n", exp.Query, exp.QueryPlan);

                queryResults.Reset();
                var tmpRes = mgr.CreateXmlResults();
                int j = 0;
                while (queryResults.HasNext())
                {
                    var xv = queryResults.NextValue();
                    using (var val = exp.Execute(xv, queryContext, queryOptions))
                    {
                        while (val.HasNext())
                        {
                            tmpRes.Add(val.NextValue());
                            j++;
                        }
                    }
                }
                msg("query returned {0} results.", j);
                // last known results, even if nothing returned
                queryResults.Dispose();
                queryResults = tmpRes;
            }
        }

        public void DeleteIndex(string ns, string nodeName, string index)
        {
#if TDS || HA
            if (env != null && env.Transactional)
            {
                containers.Peek().DeleteIndex(trans, ns, nodeName, index, updateContext);
                msg("index {0} deleted from container {1}.", index, containers.Peek().Name);
                return;
            }
#endif
            containers.Peek().DeleteIndex(ns, nodeName, index, updateContext);
            msg("index {0} deleted from container {1}.", index, containers.Peek().Name);
        }

        public void GetDocuments(string docName)
        {
            if (queryResults != null)
                queryResults.Dispose();
#if TDS || HA
            if (env != null && env.Transactional)
            {
                if (!string.IsNullOrEmpty(docName))
                {
                    queryResults = mgr.CreateXmlResults();
                    using (var lookup = mgr.CreateIndexLookup(containers.Peek(), "http://www.sleepycat.com/2002/dbxml", "name",
                                          "node-metadata-equality-string", new XmlValue(docName), IndexLookupOperation.Equal))
                    {
                        queryResults = lookup.Execute(trans, queryContext, IndexLookupOptions.CacheDocuments);
                    }
                }
                else
                {
                    queryResults = containers.Peek().GetAllDocuments(trans, GetAllDocumentOptions.None);
                }

                // everything comes back lazy - so count it the hard way.
                if (queryResults.Count < 0)
                {
                    int i = 0;
                    if (queryResults.Current != null)
                    {
                        while (queryResults.HasNext())
                        {
                            queryResults.NextValue();
                            i++;
                        }
                        queryResults.Reset();
                    }
                    msg("{0} {1} retrieved.", i, queryResults.Count == 1 ? "document" : "documents");
                }
                else
                    msg("{0} {1} retrieved.", queryResults.Count, queryResults.Count == 1 ? "document" : "documents");

                return;
            }
#endif
            if (!string.IsNullOrEmpty(docName))
            {
                queryResults = mgr.CreateXmlResults();
                using (var lookup = mgr.CreateIndexLookup(containers.Peek(), "http://www.sleepycat.com/2002/dbxml", "name",
                                      "node-metadata-equality-string", new XmlValue(docName), IndexLookupOperation.Equal))
                {
                    queryResults = lookup.Execute(queryContext, IndexLookupOptions.CacheDocuments);
                }
            }
            else
            {
                queryResults = containers.Peek().GetAllDocuments();
            }

            // it came back lazy - so count it the hard way.
            if (queryResults.Count < 0)
            {
                int i = 0;
                if (queryResults.Current != null)
                {
                    while (queryResults.HasNext())
                    {
                        queryResults.NextValue();
                        i++;
                    }
                    queryResults.Reset();
                }
                msg("{0} {1} retrieved.", i, queryResults.Count == 1 ? "document" : "documents");
            }
            else
                msg("{0} {1} retrieved.", queryResults.Count, queryResults.Count == 1 ? "document" : "documents");
        }

        public void GetMetadata(string docName)
        {
            XmlDocument doc;
#if TDS || HA
            if (env != null && env.Transactional)
            {
                doc = containers.Peek().GetDocument(trans, docName, RetrievalModes.None);

            }
            else
            {
#endif
                doc = containers.Peek().GetDocument(docName, RetrievalModes.None);
#if TDS || HA
            }
#endif
            var iter = doc.GetMetadataIterator();

            msg("Metadata for document {0}:", doc.Name);
            while (iter.Next())
            {
                msg("{0}:{1}\t{2}", string.IsNullOrEmpty(iter.Uri) ? "{}" : "{" + iter.Uri + "}", iter.Name, iter.Value);
            }
            msg(string.Empty);
        }

        public void Info(bool all)
        {
            if (!all)
            {
                msg("Container name: {0}", containers.Peek().Name);
                msg("  compression enabled: {0}", containers.Peek().CompressionEnabled);
                msg("  container state: {0}", containers.Peek().ContainerState);
                msg("  container type: {0}", containers.Peek().ContainerType);
                msg("  index nodes: {0}", containers.Peek().IndexNodes);
                msg("  page size: {0}", containers.Peek().PageSize);
                msg("  transactional: {0}", containers.Peek().Transactional);
                msg("  alias: {0}", containers.Peek().Settings.Alias);
                msg("  allow validation: {0}", containers.Peek().Settings.AllowValidation);
                msg("  checksum enabled: {0}", containers.Peek().Settings.Checksum);
#if TDS
                msg("  encrypted: {0}", containers.Peek().Settings.Encrypted);
                msg("  multiversion concurrency control (MVCC): {0}", containers.Peek().Settings.MultiVersion);
                msg("  memory mapped: {0}", containers.Peek().Settings.NoMMap);
                msg("  read-only: {0}", containers.Peek().Settings.ReadOnly);
                msg("  threaded: {0}", containers.Peek().Settings.Threaded);
#endif
#if TDS
                msg("  read uncommitted: {0}", containers.Peek().Settings.ReadUncommitted);
                msg("  non-durable transactions: {0}", containers.Peek().Settings.TransactionNotDurable);
#endif
                msg("  document id sequence increment: {0}", containers.Peek().Settings.SequenceIncrement);
                return;
            }

            foreach (var container in containers)
            {
                msg("==========================");
                msg("Container name: {0}", container.Name);
                msg("  compression enabled: {0}", container.CompressionEnabled);
                msg("  container state: {0}", container.ContainerState);
                msg("  container type: {0}", container.ContainerType);
                msg("  index nodes: {0}", container.IndexNodes);
                msg("  page size: {0}", container.PageSize);
                msg("  transactional: {0}", container.Transactional);
                msg("  alias: {0}", container.Settings.Alias);
                msg("  allow validation: {0}", container.Settings.AllowValidation);
                msg("  checksum enabled: {0}", container.Settings.Checksum);
#if TDS
                msg("  encrypted: {0}", container.Settings.Encrypted);
                msg("  multiversion concurrency control (MVCC): {0}", container.Settings.MultiVersion);
                msg("  memory mapped: {0}", container.Settings.NoMMap);
                msg("  threaded: {0}", container.Settings.Threaded);
                msg("  read-only: {0}", container.Settings.ReadOnly);
#endif
                msg("  document id sequence increment: {0}", container.Settings.SequenceIncrement);
#if TDS
                msg("  read uncommitted: {0}", container.Settings.ReadUncommitted);
                msg("  non-durable transactions: {0}", container.Settings.TransactionNotDurable);
#endif
            }
        }
        public void ListIndexes()
        {
            var spec = containers.Peek().GetIndexSpecification();

            msg("===");
            int i = 0;
            var idx = spec.Next();
            while (idx != null)
            {
                i++;
                msg("Index:   {0}\r\n\tfor node ({1}):{2}", idx.Index, idx.Namespace, idx.NodeName);
                idx = spec.Next();
            }
            msg("{0} indexes found in {1}.\r\n", i, containers.Peek().Name);
        }

        public void LookupEdgeIndex(string index, string namespaceUri, string nodeName,
            string parentNamespaceUri, string parentNodeName, string operation, string value)
        {
            var lookup = mgr.CreateIndexLookup(containers.Peek(), namespaceUri, nodeName, index, string.IsNullOrEmpty(value) ? null : new XmlValue(value),
                string.IsNullOrEmpty(operation) ? IndexLookupOperation.None : getOperation(operation));

#if TDS || HA
            if (env != null && env.Transactional)
            {
                queryResults = lookup.Execute(trans, queryContext);
                msg("lookup retrieved {0} records.", GetCount());
                return;
            }
#endif
            queryResults = lookup.Execute(queryContext);
            msg("lookup retrieved {0} records.", GetCount());
        }

        public void LookupIndex(string index, string namespaceUri, string nodeName,
            string operation, string value)
        {
            var lookup = mgr.CreateIndexLookup(containers.Peek(), namespaceUri, nodeName, index, string.IsNullOrEmpty(value) ? null : new XmlValue(value),
                string.IsNullOrEmpty(operation) ? IndexLookupOperation.None : getOperation(operation));
#if TDS || HA
            if (env != null && env.Transactional)
            {
                queryResults = lookup.Execute(trans, queryContext);
                if (queryContext.EvaluationType == EvaluationType.Eager)
                    msg("objects returned for eager index lookup '{0}': {1} objects", index, queryResults.Count);
                else
                {
                    msg("lazy index lookup '{0}' completed.", index);
                }
                return;
            }
#endif
            queryResults = lookup.Execute(queryContext);
            if (queryContext.EvaluationType == EvaluationType.Eager)
                msg("objects returned for eager index lookup '{0}': {1} objects", index, queryResults.Count);
            else
            {
                msg("lazy index lookup '{0}' completed.", index);
            }
        }

        public void LookupStatistics(string index, string namespaceUri, string nodeName,
            string parentNamespaceUri, string parentNodeName, string value)
        {
            KeyStatistics stats;
            if (string.IsNullOrEmpty(parentNamespaceUri))
            {
#if TDS || HA
                if (env != null && env.Transactional)
                {
                    stats = containers.Peek().LookupStatistics(trans, namespaceUri, nodeName, index,
                                                               string.IsNullOrEmpty(value) ? null : new XmlValue(value));
                }
                else
                {
#endif
                    stats = containers.Peek().LookupStatistics(namespaceUri, nodeName, index,
                                                               string.IsNullOrEmpty(value) ? null : new XmlValue(value));
#if TDS || HA
                }
#endif
            }
            else
            {
#if TDS || HA
                if (env != null && env.Transactional)
                {
                    stats = containers.Peek().LookupStatistics(trans, namespaceUri, nodeName, parentNamespaceUri, parentNodeName,
                        index, string.IsNullOrEmpty(value) ? null : new XmlValue(value));
                }
                else
                {
#endif
                    stats = containers.Peek().LookupStatistics(namespaceUri, nodeName, parentNamespaceUri, parentNodeName,
                        index, string.IsNullOrEmpty(value) ? null : new XmlValue(value));
#if TDS || HA
                }
#endif
            }

            msg("Number of indexed keys: {0} Number of unique keys: {1} Sum key value size: {2}", (long)stats.IndexedKeys, stats.UniqueKeys, (long)stats.SumKeyValueSize);
        }

        private int GetCount()
        {
            if (queryResults.Count >= 0) return queryResults.Count;

            var i = 0;
            while (queryResults.HasNext())
            {
                queryResults.NextValue();
                i++;
            }
            queryResults.Reset();
            return i;
        }

        private static IndexLookupOperation getOperation(string op)
        {
            switch (op.Trim())
            {
                case ">":
                    return IndexLookupOperation.GreaterThan;
                case ">=":
                case "=>":
                    return IndexLookupOperation.GreaterThanOrEqual;
                case "<":
                    return IndexLookupOperation.LessThan;
                case "<=":
                case "=<":
                    return IndexLookupOperation.LessThanOrEqual;
                default:
                    return IndexLookupOperation.Equal;
            }
        }

        public void PutDocuments(string filesPath, string filter)
        {
            if (containers.Count < 1)
            {
                Warn("You must open a container first.");
                return;
            }
            if (string.IsNullOrEmpty(filter)) filter = "*.xml";

            var files = Directory.GetFiles(filesPath, filter, SearchOption.TopDirectoryOnly);
#if TDS
            XmlTransaction t = null;
            if (env != null && env.Transactional)
                t = trans.CreateChild(TransactionType.SyncTransaction);
#endif
            foreach (var file in files)
            {
                Verbose("inserting document {0}...", Path.GetFileNameWithoutExtension(file));
#if TDS
                if (env != null && env.Transactional)
                {
                    containers.Peek().PutDocument(t, file, updateContext);
                }
                else
                {
#endif
                    containers.Peek().PutDocument(file, updateContext);
#if TDS
                }
#endif
            }
#if TDS
            if (t != null)
            {
                Verbose("Committing child transaction...");
                t.Commit(true);
            }
#endif
            Verbose("syncing container {0}...", containers.Peek().Name);
            containers.Peek().Sync();
            msg("{0} documents inserted into {1} container.", files.Length, containers.Peek().Name);
        }

        public void PutDocumentByFile(string filePath)
        {
#if TDS
            if (env != null && trans != null)
            {
                containers.Peek().PutDocument(trans, filePath, updateContext);
                return;
            }
#endif
            containers.Peek().PutDocument(filePath, updateContext, PutDocumentOptions.None);
        }

        public void PutDocumentByString(string contents, string name)
        {
#if TDS
            if (env != null && trans != null)
            {
                containers.Peek().PutDocument(trans, name, contents, updateContext,
                                              string.IsNullOrEmpty(name)
                                                  ? PutDocumentOptions.GenerateFileName
                                                  : PutDocumentOptions.None);
                return;
            }
#endif
            containers.Peek().PutDocument(name, contents, updateContext,
                                          string.IsNullOrEmpty(name)
                                              ? PutDocumentOptions.GenerateFileName
                                              : PutDocumentOptions.None);
        }

        public void PutDocumentByQuery(string query, string name)
        {
#if TDS
            if (env != null && trans != null)
            {
                var results = mgr.Query(trans, query, queryContext);
                if (results.Count > 0)
                {
                    msg("Query returned {0} results.", results.Count);
                    do
                    {
                        containers.Peek().PutDocument(trans, results.NextDocument(), updateContext,
                                                      results.Count > 1
                                                          ? PutDocumentOptions.GenerateFileName
                                                          : PutDocumentOptions.None);
                    } while (results.MoveNext());
                }
                else
                {
                    msg("Query returned 0 results.");
                }
                return;
            }
            var res = mgr.Query(trans, query, queryContext);

#else
            var res = mgr.Query(query, queryContext);
#endif
            if (res.Count > 0)
            {
                msg("Query returned {0} results.", res.Count);
                do
                {
#if TDS
                    if (env != null && env.Transactional)
                    {
                        containers.Peek().PutDocument(trans, res.NextDocument(), updateContext,
                                                      res.Count > 1
                                                          ? PutDocumentOptions.GenerateFileName
                                                          : PutDocumentOptions.None);
                    }
                    else
                    {
#endif
                        containers.Peek().PutDocument(res.NextDocument(), updateContext,
                                                      res.Count > 1
                                                          ? PutDocumentOptions.GenerateFileName
                                                          : PutDocumentOptions.None);
#if TDS
                    }
#endif
                } while (res.MoveNext());
            }
            else
            {
                msg("Query returned 0 results.");
            }
        }

        public void QueryFile(string file)
        {
            if (string.IsNullOrEmpty(file)) throw new ValidationException("Must specify a XQuery file.");

            if (!File.Exists(file)) throw new ValidationException("Must specify an XQuery file with a valid path.");
            
            Query(File.ReadAllText(file));

        }

        public void Query (string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                if (queryExpression == null) throw new ValidationException("No query provided or prepared.");

#if TDS || HA
                if (env != null && env.Transactional)
                {
                    queryResults = queryExpression.Execute(trans, queryContext, queryOptions);
                }
                else
                {
#endif
                    queryResults = queryExpression.Execute(queryContext, queryOptions);
#if TDS || HA
                }
#endif
            }
            else
            {
#if TDS || HA
                if (env != null && env.Transactional)
                {
                    queryResults = mgr.Query(trans, query, queryContext);
                }
                else{
#endif
                    queryResults = mgr.Query(query, queryContext);
#if TDS || HA
                }
#endif
            }

            msg("{0} returned {1} {2}.", queryExpression != null ? "prepared query '" + queryExpression.Query : "query " + query, GetCount(),GetCount() == 1 ? "results" : "result(s)"); 
        }

        public void QueryPlan(string query, string queryPlanPath)
        {
            if (!string.IsNullOrEmpty(query)) Prepare(query);
            var tr = new StringReader(queryExpression.QueryPlan);
            XDocument doc = XDocument.Load(tr);
            Verbose("\r\nQuery Plan:\r\n{0}\r\n", doc.ToString(SaveOptions.None));

            if (string.IsNullOrEmpty(queryPlanPath)) return;

            if (!Path.IsPathRooted(queryPlanPath))
                queryPlanPath = Path.Combine(path, queryPlanPath);
            doc.Save(queryPlanPath,SaveOptions.None);
            msg("Query plan saved to '{0}'.\r\n",queryPlanPath);
        }

        public void ReindexContainer(string container, string option)
        {
#if TDS || HA
            if (env != null && env.Transactional)
            {
                mgr.ReindexContainer(trans,container,updateContext,option.ToLower().Equals("n") ? ReindexOptions.IndexNodes : ReindexOptions.NoIndexNodes);
                msg("Container reindexed: {0}\r\n", container);
                return;
            }
#endif
            mgr.ReindexContainer(container, updateContext, option.ToLower().Equals("n") ? ReindexOptions.IndexNodes : ReindexOptions.NoIndexNodes);
            msg("Container reindexed: {0}\r\n", container);
        }

        public void RemoveAlias(string alias)
        {
            foreach (Container container in containers)
            {
                if (container.RemoveAlias(alias))
                {
                    msg("removed alias '{0}' from container '{1}'.", alias, container.Name);
                    return;
                }
            }
            msg("alias '{0}' does not exist on any container.");
        }

        public void RemoveContainer(string containerName)
        {
            // remove the container from the stack
            var c2 = new Container[containers.Count];
            containers.CopyTo(c2, 0);

            for (int i = 0; i < c2.Length; i++)
            {
                if (containerName.Equals(c2[i].Name.Trim()))
                {
                    c2[i].Dispose();
                    c2[i] = null;
#if DEBUG
                    //force the container instance to die if in debug mode
                    GC.Collect(0, GCCollectionMode.Forced);
#endif
                }
            }

            containers.Clear();

            for (int i = c2.Length - 1; i >= 0; i--)
            {
                if (c2[i] != null)
                    containers.Push(c2[i]);
            }

#if TDS || HA
            if (env != null && env.Transactional)
            {
                mgr.RemoveContainer(trans, containerName);
                //mgr.RemoveContainer(containerName);
                msg("Container removed: {0}", containerName);
                return;
            }
#endif
            mgr.RemoveContainer(containerName);
            msg("Container removed: {0}", containerName);
        }

        public void RemoveDocument(string docName)
        {
#if TDS
            if (env != null && env.Transactional)
            {
                containers.Peek().DeleteDocument(trans,docName,updateContext);
            }
            else
            {
#endif
                containers.Peek().DeleteDocument(docName, updateContext);
#if TDS
            }
#endif
            msg("document '{0}' deleted from container '{1}'.\r\n",docName, containers.Peek().Name);
        }

        public void SetAutoIndexing(bool indexing)
        {
            if (containers.Count == 0)
                throw new ValidationException("must have the container open you wish to set auto-indexing on!");

            using (var spec = containers.Peek().GetIndexSpecification())
            {
                if (spec.AutoIndexing == indexing)
                {
                    msg("auto-indexing for container '{0}' already set to {1}", containers.Peek().Name, indexing);
                    return;
                }
                spec.AutoIndexing = indexing;                
#if TDS
                if (env != null && env.Transactional)
                {
                    containers.Peek().SetIndexSpecification(trans, spec, updateContext);
                    msg("auto-indexing for container '{0}' now set to {1}",containers.Peek().Name, indexing);
                    return;
                }
#endif
                containers.Peek().SetIndexSpecification(spec, updateContext);
                msg("auto-indexing for container '{0}' now set to {1}", containers.Peek().Name, indexing);
            }
        }

        public void SetBaseUri(string baseUri)
        {
            if (string.IsNullOrEmpty(baseUri))
            {
                msg("Base URI = '{0}'", queryContext.BaseUri);
            }
            else
            {
                queryContext.BaseUri = baseUri;
                msg("Current Base URI: '{0}'.",baseUri);
            }
        }

        public void SetLazy(bool lazy)
        {
            queryContext.EvaluationType = lazy ? EvaluationType.Lazy : EvaluationType.Eager;
            msg("Evaluation type set to {0}.\r\n", lazy ? EvaluationType.Lazy : EvaluationType.Eager);
        }

        public void SetMetadata(string docName, string uri, string name, string value)
        {
            XmlDocument doc;
#if TDS
            if (env != null && env.Transactional)
            {
                doc = containers.Peek().GetDocument(trans, docName, RetrievalModes.None);
                doc.SetMetadata(uri,name,new XmlValue(value));
                containers.Peek().UpdateDocument(trans, doc, updateContext);
                msg("Metadata item '{0}:{1}' added to document {2}",uri,name,docName);
            }
#endif
            doc = containers.Peek().GetDocument(docName, RetrievalModes.None);
            doc.SetMetadata(uri, name, new XmlValue(value));
            containers.Peek().UpdateDocument(doc, updateContext);
            msg("Metadata item '{0}:{1}' added to document {2}", uri, name, docName);
        }

        public void SetNamespace(string prefix, string ns)
        {
            queryContext.SetNamespace(prefix, ns);
        }

        public void SetDocumentProjection(bool project)
        {
            if (project)
            {
                queryOptions |= QueryOptions.DocumentProjection;
                msg("Document projection enabled.\r\n");
            }
            else
            {                
                queryOptions ^= QueryOptions.DocumentProjection;
                msg("Document projection disabled.\r\n");
            }
        }

        public void SetQueryTimeout(uint seconds)
        {
            queryContext.QueryTimeoutSeconds = seconds;
            msg("Setting query timeout to {0} seconds\r\n",seconds);
        }

        public void SetVariable(string name, string varValue)
        {
            queryContext.SetVariableValue(name,varValue);
            msg("Setting ${0} = {1}\r\n",name,varValue);
        }

        public void SetVerbose(int category, int level)
        {
            var cat = LogConfigurationCategory.None;
            LogConfigurationLevel lev;
            switch (category)
            {
                case -1:
                    cat = LogConfigurationCategory.All;
                    break;
                case 0:
                    LogConfiguration.SetCategory(LogConfigurationCategory.None, true);
                    break;
                default:
                    cat = (LogConfigurationCategory)Enum.ToObject(typeof(LogConfigurationCategory),category);
                    break;
            }

            switch (level)
            {
                case -1:
                    lev = LogConfigurationLevel.All;
                    break;
                case 0:
                    lev = (LogConfigurationLevel) Enum.ToObject(typeof (LogConfigurationLevel), level);
                    break;
                default:
                    lev = (LogConfigurationLevel)Enum.ToObject(typeof(LogConfigurationLevel), level);
                    break;
            }

            if (level == 0 || category == 0) return;
            
            LogConfiguration.SetCategory(cat, true);
            LogConfiguration.SetLogLevel(lev, true);
        }

        public void Sync()
        {
            foreach (Container container in containers)
            {
                Verbose("syncing {0}...",container.Name);
                container.Sync();
            }
            msg("containers synced.");
        }

        public void UpgradeContainer(string containerName)
        {
            mgr.UpgradeContainer(containerName, updateContext);
            msg("container {0} upgraded.", containerName);
        }

        #region internal stuff

        private void SetWorkingDirectory()
        {
            var envPath = Environment.GetEnvironmentVariable(DbxmlResources.DbxmlAllUpper);

            Console.WriteLine();
            if (!string.IsNullOrEmpty(opts.HomeDirectory))
            {
                msg(DbxmlResources.WorkingDirectorySetTo0, opts.HomeDirectory);
                path = opts.HomeDirectory;
            }
            else if (!string.IsNullOrEmpty(envPath))
            {
                msg(DbxmlResources.WorkingDir2);
                msg(DbxmlResources.WorkingDirSetToDbxmlEnv0, envPath);
                path = envPath;
            }
            else
            {
                msg(DbxmlResources.EnvironmentVariableNotSet);
                msg(DbxmlResources.WorkingDirectorySetTo0, Environment.CurrentDirectory);
                path = Environment.CurrentDirectory;
            }

            // set our current working directory here!
            Environment.CurrentDirectory = path;
        }

        private static void msg(string msg, params object[] args)
        {
            if (args != null)
            {
                Console.WriteLine(msg, args);
                return;
            }
            Console.WriteLine(msg);
        }

        private void Verbose(string msg, params object[] args)
        {
            if (!opts.Verbose) return;
            if (args != null)
            {
                Console.WriteLine(msg, args);
                return;
            }

            Console.WriteLine(msg);
        }

        protected void Warn(string msg, params object[] args)
        {
            var priorback = Console.BackgroundColor;
            var priorfore = Console.ForegroundColor;
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(msg, args);
            Console.BackgroundColor = priorback;
            Console.ForegroundColor = priorfore;
            Console.WriteLine();
        }

        protected void Error(string msg, params object[] args)
        {
            var priorback = Console.BackgroundColor;
            var priorfore = Console.ForegroundColor;
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(msg, args);
            Console.BackgroundColor = priorback;
            Console.ForegroundColor = priorfore;
            Console.WriteLine();
        }

        #endregion internal stuff

        #region IDisposable Members

        public void Dispose()
        {
            if (queryExpression != null)
            {
                queryExpression.Dispose();
                queryExpression = null;
            }

            if (queryResults != null)
                queryResults.Dispose();

            while (containers.Count > 0)
            {
                var c = containers.Pop();
                c.Dispose();
            }
#if TDS || HA
            if (trans != null)
            {
                trans.Abort();
            }
#endif
            if (mgr != null) mgr.Dispose();
#if TDS || HA || CDS
            if (env != null)
            {
                env.Close();
                env.Dispose();
            }

#endif
        }

        #endregion IDisposable Members
    }
}