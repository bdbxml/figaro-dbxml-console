/*************************************************************************************************
* 
* THIS CODE AND INFORMATION ARE PROVIDED AS IS WITHOUT WARRANTY OF ANY
* KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
* IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
* 
*************************************************************************************************/
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using Figaro.Utilities.Common;
using Figaro.Utilities.Resources;

namespace Figaro.Utilities.Commands
{
    public class AbortCommand : BaseCommand
    {
        public AbortCommand() : base(CommandResources.abort) { }

        public void Execute(FigaroContext context)
        {
            ctx = context;
            if (FigaroProductInfo.ProductEdition != FigaroProductEdition.TransactionalDataStore &&
                FigaroProductInfo.ProductEdition != FigaroProductEdition.HighAvailability)
            {
                Warn(DbxmlResources.MustUseTDS);
                throw new ValidationException();
            }
            context.Abort();
        }
    }
    public class AddAliasCommand : BaseCommand
    {
        public AddAliasCommand() : base(CommandResources.addalias) { }

        public new void Execute(FigaroContext context, string args)
        {
            base.Execute(context, args);

            if (argv.Length == 2)
            {
                context.AddAlias(argv[1],argv[0]);
                return;
            }
            if (argv.Length != 1) return;
            context.AddAlias(argv[0]);
            return;
        }
    }
    public class AddIndexCommand : BaseCommand
    {
        public AddIndexCommand() : base(CommandResources.addindex) { }

        public new void Execute(FigaroContext context, string args)
        {
            base.Execute(context, args);
            var uri = string.Empty;
            var node = string.Empty;

            if (Uri.IsWellFormedUriString(argv[0], UriKind.Absolute))
            {
                uri = argv[0];
            }
            else
            {
                if (!IndexingStrategy.IsValidIndexingStrategy(argv[0]))
                    node = argv[0];
            }

            var idx = from a in argv
                      where IndexingStrategy.IsValidIndexingStrategy(a)
                      select a;

            foreach (var index in idx)
            {
                context.AddIndex(uri, node, index);
            }
        }
    }

    public class CloseCommand: BaseCommand
    {
        public CloseCommand() : base(CommandResources.close) { }
        public new void Execute(FigaroContext context, string args)
        {
            if (string.IsNullOrEmpty(args))
            {
                Warn("Closing all open containers.");
                context.Close(string.Empty);
                return;
            }

            base.Execute(context, args);
            
            context.Close(argv[0]);
        }
    }
    public class CommitCommand : BaseCommand
    {
        public CommitCommand() : base(CommandResources.commit) { }

        public new void Execute(FigaroContext context, string args)
        {
            ctx = context;
            if (FigaroProductInfo.ProductEdition != FigaroProductEdition.TransactionalDataStore &&
                FigaroProductInfo.ProductEdition != FigaroProductEdition.HighAvailability)
            {
                Warn(DbxmlResources.MustUseTDS);
                throw new ValidationException();
            }

            context.Commit();
        }
    }
    public class CompactContainerCommand : BaseCommand
    {
        public CompactContainerCommand() : base(CommandResources.compactcontainer) { }

        public new void Execute(FigaroContext context, string args)
        {
            base.Execute(context, args);
            context.CompactContainer(argv[0]);
        }
    }
    public class ContextQueryCommand : BaseCommand
    {
        public ContextQueryCommand() : base(CommandResources.contextquery) { }

        public new void Execute(FigaroContext context, string args)
        {
            base.Execute(context, args);
            context.ContextQuery(argv[0]);
        }
    }
    public class CQueryCommand : BaseCommand
    {
        public CQueryCommand() : base(CommandResources.cquery) { }

        public new void Execute(FigaroContext context, string args)
        {
            base.Execute(context, args);
            context.CQuery(argv[0]);
        }
    }
    public class CreateContainerCommand : BaseCommand
    {
        public CreateContainerCommand() : base(CommandResources.createcontainer) { }

        public new void Execute(FigaroContext context, string args)
        {
            try
            {
                if (string.IsNullOrEmpty(args))
                {
                    context.CreateContainer(string.Empty, "in", false);
                    return;
                }
                base.Execute(context, args);
                ValidateArgCount(1, 3);
                //ValidatePath(argv[0]);
                bool validate = false;
                string ctyp = string.Empty;
                string w = string.Empty;
                if (argv.Length > 1)
                {
                    ValidateLiteral(argv[1].ToLower(), new[] { "n", "in", "d", "id" });
                    ctyp = argv[1].ToLower();
                    switch (ctyp)
                    {
                        case "n":
                            w = "with node storage enabled.";
                            break;
                        case "in":
                            w = "with indexed node storage enabled.";
                            break;
                        case "d":
                            w = "with Wholedoc storage enabled.";
                            break;
                        case "id":
                            w = "with indexed Wholedoc storage enabled.";
                            break;
                    }
                }
                else
                {
                    w = "with default container settings enabled.";
                }

                if (argv.Length > 2)
                {
                    ValidateLiteral(argv[2], new[] { "validate", "novalidate" });
                    validate = argv[2].Equals("validate");
                }

                context.CreateContainer(argv[0], ctyp, validate);
                Msg("created container {0} {1}", argv[0], w);
            }
            catch (ValidationException)
            {
                return;
            }
        }
    }
    public class DeleteIndexCommand : BaseCommand
    {
        public DeleteIndexCommand() : base(CommandResources.delindex) { }

        public new void Execute(FigaroContext context, string args)
        {
            base.Execute(context, args);
            var uri = string.Empty;
            var node = string.Empty;

            if (Uri.IsWellFormedUriString(argv[0], UriKind.Absolute))
            {
                uri = argv[0];
            }
            else
            {
                if (!IndexingStrategy.IsValidIndexingStrategy(argv[0]))
                    node = argv[0];
            }

            var idx = from a in argv
                      where IndexingStrategy.IsValidIndexingStrategy(a)
                      select a;

            foreach (var index in idx)
            {
                context.DeleteIndex(uri, node, index);
            }
        }
    }
    public class GetDocumentsCommand : BaseCommand
    {
        public GetDocumentsCommand() : base(CommandResources.getdocuments) { }

        public new void Execute(FigaroContext context, string args)
        {
            if (string.IsNullOrEmpty(args))
            {
                ctx = context;
                context.GetDocuments(string.Empty);
                return;
            }

            base.Execute(context, args);

            context.GetDocuments(argv[0]);
            return;
        }
    }
    public class GetMetadataCommand : BaseCommand
    {
        public GetMetadataCommand() : base(CommandResources.getmetadata) { }

        public new void Execute(FigaroContext context, string args)
        {
            base.Execute(context, args);
            context.GetMetadata(argv[0]);
        }

    }
    public class HelpCommand : BaseCommand
    {
        public HelpCommand() : base(CommandResources.help) { }

        #region ICommand Members

        public void Execute(string args)
        {
            if (string.IsNullOrEmpty(args))
            {
                Msg(string.Empty);
                Msg("dbxml commands");
                Msg("=".PadRight(Console.WindowWidth - 1, '='));
                var rs =
                    CommandResources.ResourceManager.GetResourceSet(CultureInfo.InvariantCulture, false, false).
                        OfType<DictionaryEntry>().OrderBy(i=> i.Key);

                foreach (var resourceSet in rs)
                {
                    if (string.IsNullOrEmpty(resourceSet.Key.ToString())) continue;
                    Msg("{0}{1}",
                        // ReSharper disable PossibleNullReferenceException
                        CommandResources.ResourceManager.GetString(resourceSet.Key.ToString()).PadRight(18, ' '),
                        // ReSharper restore PossibleNullReferenceException
                        CommandSummaryResources.ResourceManager.GetString(resourceSet.Key.ToString()));
                }

                Msg(string.Empty);
                return;
            }

            if (string.IsNullOrEmpty(CommandResources.ResourceManager.GetString(args.Trim())))
            {
                Msg("command not found: {0}", args.Trim());
                Msg(string.Empty);
                return;
            }

            Msg(string.Empty);
            Msg("{0} - {1}",
                CommandResources.ResourceManager.GetString(args.Trim()),
                CommandSummaryResources.ResourceManager.GetString(args.Trim()));
            Msg("Usage: {0}",
                CommandUsageResources.ResourceManager.GetString(args.Trim()));
            Msg(string.Empty);
            Msg(CommandDetailResources.ResourceManager.GetString(args.Trim()));
            Msg(string.Empty);
        }

        #endregion ICommand Members
    }
    public class InfoCommand : BaseCommand
    {
        public InfoCommand() : base(CommandResources.info) { }

        public new void Execute(FigaroContext context, string args)
        {
            if (string.IsNullOrEmpty(args))
            {
                context.Info(false);
                return;
            }

            base.Execute(context, args);
            context.Info(true);
            return;

        }
    }
    public class ListIndexesCommand : BaseCommand
    {
        public ListIndexesCommand() : base(CommandResources.listindexes) { }

        public new void Execute(FigaroContext context, string args)
        {
            //base.Execute(context, args);
            context.ListIndexes();
        }
    }
    public class LookupEdgeIndexCommand : BaseCommand
    {
        public LookupEdgeIndexCommand() : base(CommandResources.lookupedgeindex) { }

        public new void Execute(FigaroContext context, string args)
        {
            args = args.Replace("\"\"", "{blank}");
            args = args.Replace("''", "{blank}");

            base.Execute(context, args);

            ValidateArgCount(5, 7);
            ValidateIndexDescription(argv[0]);
            if (argv.Length == 5)
            {
                context.LookupEdgeIndex(
                    argv[0].Replace("{blank}", string.Empty),
                    argv[1].Replace("{blank}", string.Empty),
                    argv[2].Replace("{blank}", string.Empty),
                    argv[3].Replace("{blank}", string.Empty),
                    argv[4].Replace("{blank}", string.Empty),
                    string.Empty,
                    string.Empty
                    );
            }
            else if (argv.Length == 7)
            {
                context.LookupEdgeIndex(
                    argv[0].Replace("{blank}", string.Empty),
                    argv[1].Replace("{blank}", string.Empty),
                    argv[2].Replace("{blank}", string.Empty),
                    argv[3].Replace("{blank}", string.Empty),
                    argv[4].Replace("{blank}", string.Empty),
                    argv[5].Replace("{blank}", string.Empty),
                    argv[6].Replace("{blank}", string.Empty)
                    );
            }
            else
            {
                throw new ValidationException("Invalid number of arguments.");
            }
        }
    }
    public class LookupIndexCommand : BaseCommand
    {
        public LookupIndexCommand() : base(CommandResources.lookupindex) { }

        public new void Execute(FigaroContext context, string args)
        {
            args = args.Replace("\"\"", "{blank}");
            args = args.Replace("''", "{blank}");

            base.Execute(context, args);
            ValidateArgCount(3, 5);
            ValidateIndexDescription(argv[0]);
            if (argv.Length == 5)
            {
                context.LookupIndex(
                    argv[0].Replace("{blank}", string.Empty),
                    argv[1].Replace("{blank}", string.Empty),
                    argv[2].Replace("{blank}", string.Empty),
                    argv[3].Replace("{blank}", string.Empty),
                    argv[4].Replace("{blank}", string.Empty)
                    );
            }
            else if (argv.Length == 3)
            {
                context.LookupIndex(
                    argv[0].Replace("{blank}", string.Empty),
                    argv[1].Replace("{blank}", string.Empty),
                    argv[2].Replace("{blank}", string.Empty),
                    string.Empty,
                    string.Empty
                    );
            }
            else
            {
                throw new ValidationException("Invalid number of arguments.");
            }
        }
    }
    public class LookupStatisticsCommand : BaseCommand
    {
        public LookupStatisticsCommand() : base(CommandResources.lookupstats) { }

        public new void Execute(FigaroContext context, string args)
        {
            args = args.Replace("\"\"", "{blank}");
            args = args.Replace("''", "{blank}");

            base.Execute(context, args);
            ValidateArgCount(3, 6);

            if (argv.Length == 3)
            {
                context.LookupStatistics(argv[0].Replace("{blank}", string.Empty),
                    argv[1].Replace("{blank}", string.Empty),
                    argv[2].Replace("{blank}", string.Empty), string.Empty, string.Empty, string.Empty);
            }
            if (argv.Length == 6)
            {
                context.LookupStatistics(argv[0].Replace("{blank}", string.Empty),
                    argv[1].Replace("{blank}", string.Empty),
                    argv[2].Replace("{blank}", string.Empty),
                    argv[3].Replace("{blank}", string.Empty),
                    argv[4].Replace("{blank}", string.Empty),
                    argv[5].Replace("{blank}", string.Empty));
            }
            else
            {
                throw new ValidationException();
            }
        }
    }
    public class OpenContainerCommand : BaseCommand
    {
        public OpenContainerCommand() : base(CommandResources.opencontainer) { }

        public new void Execute(FigaroContext context, string args)
        {
            try
            {
                base.Execute(context, args);
                bool validate = false;
                ValidateArgCount(1, 2);
                //ValidatePath(argv[0]);
                if (argv.Length > 1)
                {
                    ValidateLiteral(argv[1], new[] { "validate", "novalidate" });
                    validate = argv[1].Equals("validate");
                }
                context.OpenContainer(argv[0], validate);
            }
            catch (ValidationException)
            {
                return;
            }
            if (string.IsNullOrEmpty(args))
            {
                WarnUsage();
                return;
            }

            if (argv.Length == 0)
            {
                WarnUsage();
                return;
            }

            return;
        }
    }
    public class PreloadCommand : BaseCommand
    {
        public PreloadCommand() : base(CommandResources.preload) { }

        public new void Execute(FigaroContext context, string args)
        {
            base.Execute(context, args);
            context.Preload(argv[0]);
        }
    }
    public class PrepareCommand : BaseCommand
    {
        public PrepareCommand() : base(CommandResources.prepare) { }

        public new void Execute(FigaroContext context, string args)
        {
            base.Execute(context, args);
            context.Prepare(argv[0]);
        }
    }
    public class PrintCommand : BaseCommand
    {
        public PrintCommand() : base(CommandResources.print) { }

        public new void Execute(FigaroContext context, string args)
        {
            if (string.IsNullOrEmpty(args.Trim()))
            {
                ctx = context;
                context.PrintResults(0, string.Empty);
                return;
            }
            base.Execute(context, args);

            if (argv.Length == 0)
            {
                context.PrintResults(0, string.Empty);
                return;
            }

            if (argv[0].ToLower().Equals("printnames"))
            {
                if (argv.Length == 3)
                {
                    context.PrintNames(int.Parse(argv[1]), argv[2]);
                    return;
                }
                if (argv.Length == 2)
                {
                    int i;
                    if (int.TryParse(argv[1], out i))
                    {
                        context.PrintNames(i, string.Empty);
                        return;
                    }
                    context.PrintNames(0, argv[2]);
                    return;
                }
                context.PrintNames(0, string.Empty);
                return;
            }

            if (argv.Length == 2)
            {
                context.PrintResults(int.Parse(argv[0]), argv[1]);
                return;
            }

            int j;
            if (int.TryParse(argv[0], out j))
            {
                context.PrintResults(j, string.Empty);
                return;
            }
            context.PrintResults(0, argv[0]);
        }
    }
    public class PutDocumentCommand : BaseCommand
    {
        public PutDocumentCommand() : base(CommandResources.putdocument) { }

        public new void Execute(FigaroContext context, string args)
        {
            base.Execute(context, args);

            if (argv.Length == 3)
            {
                switch (argv[2])
                {
                    case "f":
                        context.PutDocumentByFile(argv[0]);
                        return;
                    case "s":
                        context.PutDocumentByString(argv[0], argv[1].Replace("{", string.Empty).Replace("}", string.Empty));
                        return;
                    case "q":
                        context.PutDocumentByQuery(argv[0], argv[1]);
                        return;
                }
            }
            else if (argv.Length == 2)
            {
                var res = from opt in new[] { "f", "s", "q" }
                          where argv[1].Equals(opt)
                          select opt;
                if (res.Count() > 0)
                {
                    PutDoc(res.First());
                }
                else
                {
                    context.PutDocumentByString(argv[0], argv[1]);
                }
            }
            else if (argv.Length == 1)
            {
                ctx.PutDocumentByString(argv[0], string.Empty);
            }
            else
            {
                WarnUsage();
                throw new ValidationException();
            }
        }

        private void PutDoc(string opt)
        {
            switch (opt)
            {
                case "f":
                    ctx.PutDocumentByFile(argv[0]);
                    return;
                case "s":
                    ctx.PutDocumentByString(argv[0], argv[1]);
                    return;
                case "q":
                    ctx.PutDocumentByQuery(argv[0], argv[1]);
                    return;
            }
        }
    }
    public class PutDocumentsCommand : BaseCommand
    {
        public PutDocumentsCommand() : base(CommandResources.putdocuments) { }

        public new void Execute(FigaroContext context, string args)
        {
            base.Execute(context, args);

            ValidatePath(argv[0]);
            var pattern = string.Empty;
            if (argv.Length > 1)
                pattern = argv[1];
            context.PutDocuments(argv[0], pattern);
        }
    }


    public class QueryCommand : BaseCommand
    {
        public QueryCommand() : base(CommandResources.query) { }

        public new void Execute(FigaroContext context, string args)
        {
            try
            {
                base.Execute(context, args);
                if (argv.Length > 1)
                {
                    if (argv[1].Equals("f"))
                        context.QueryFile(argv[0]);
                    else
                    {
                        Error("Invalid option '{0}'. Specify 'f' if you wish to query using an XQuery file.");
                        return;
                    }
                    return;
                }

                context.Query(argv[0]);
            }
            catch (IndexOutOfRangeException)
            {
                Error(DbxmlResources.QueryError);
            }
            catch (NullReferenceException)
            {
                Error(DbxmlResources.QueryError);
            }
        }
    }

    public class QueryPlanCommand : BaseCommand
    {
        public QueryPlanCommand() : base(CommandResources.queryplan) { }

        public new void Execute(FigaroContext context, string args)
        {
            if (string.IsNullOrEmpty(args))
            {
                context.QueryPlan(string.Empty, string.Empty);
                return;
            }

            args = args.Replace("\"\"", "{blank}");
            args = args.Replace("''", "{blank}");

            base.Execute(context, args);
            context.QueryPlan(
                argv[0].Replace("{blank}",string.Empty),
                argv.Length > 1 ? argv[1] : string.Empty
                );
        }
    }

    public class ReindexContainerCommand : BaseCommand
    {
        public ReindexContainerCommand() : base(CommandResources.reindexcontainer) { }
        public new void Execute(FigaroContext context, string args)
        {
            base.Execute(context, args);
            ValidateArgCount(2, 2);
            context.ReindexContainer(argv[0],argv[1]);
        }
    }

    public class RemoveAliasCommand : BaseCommand
    {
        public RemoveAliasCommand() : base(CommandResources.removealias) { }

        public new void Execute(FigaroContext context, string args)
        {
            base.Execute(context, args);
            ValidateArgCount(1, 1);
            
            if (argv.Length == 1)
            {
                context.RemoveAlias(argv[0]);
            }
        }
    }

    public class RemoveContainerCommand : BaseCommand
    {
        public RemoveContainerCommand() : base(CommandResources.removecontainer) { }

        public new void Execute(FigaroContext context, string args)
        {
            base.Execute(context, args);
            context.RemoveContainer(argv[0]);
        }
    }
    
    public class RemoveDocumentCommand : BaseCommand
    {
        public RemoveDocumentCommand() : base(CommandResources.removedocument) { }

        public new void Execute(FigaroContext context, string args)
        {
            base.Execute(context, args);
            context.RemoveDocument(argv[0]);
            return;
        }
    }

    public class RunCommand : BaseCommand
    {
        public RunCommand() : base(CommandResources.run) { }

        public string Script { get; private set; }
        public new void Execute(FigaroContext context, string args)
        {
            if (string.IsNullOrEmpty(args))
            {
                WarnUsage();
                throw new ValidationException("no script specified.");
            }
            base.Execute(context, args);
            argv[0] = Path.GetFullPath(argv[0]);
            Msg("running script '{0}'...",argv[0]);
            Script = argv[0];
        }
    }

    public class SetAutoIndexingCommand : BaseCommand
    {
        public SetAutoIndexingCommand() : base(CommandResources.setautoindexing) { }

        public new void Execute(FigaroContext context, string args)
        {
            try
            {
                if (args.ToLower().Equals("on"))
                    context.SetAutoIndexing(true);
                else if (args.ToLower().Equals("off"))
                    context.SetAutoIndexing(false);
                else
                {
                    throw new ValidationException("command only accepts 'on' or 'off' as a parameter.");
                }
                return;
            }
            catch(ValidationException ve)
            {
                Warn(ve.Message);
                WarnUsage();
                return;
            }
        }
    }

    public class SetBaseUriCommand : BaseCommand
    {
        public SetBaseUriCommand() : base(CommandResources.setbaseuri) { }

        public new void Execute(FigaroContext context, string args)
        {
            context.SetBaseUri(args);
        }
    }

    public class SetLazyCommand : BaseCommand
    {
        public SetLazyCommand() : base(CommandResources.setlazy) { }

        public new void Execute(FigaroContext context, string args)
        {
            try
            {
                if (args.ToLower().Equals("on"))
                    context.SetLazy(true);
                else if (args.ToLower().Equals("off"))
                    context.SetLazy(false);
                else
                {
                    throw new ValidationException("command only accepts 'on' or 'off' as a parameter.");
                }
                return;
            }
            catch (ValidationException ve)
            {
                Warn(ve.Message);
                WarnUsage();
                return;
            }
        }
    }

    public class SetMetadataCommand : BaseCommand
    {
        public SetMetadataCommand() : base(CommandResources.setlazy) { }

        public new void Execute(FigaroContext context, string args)
        {
            try
            {
                base.Execute(context, args);
                ValidateArgCount(4,4);
                context.SetMetadata(argv[0],argv[1],argv[2],argv[3]);
            }
            catch (ValidationException ve)
            {
                Warn(ve.Message);
                WarnUsage();
                return;
            }
        }
    }

    public class SetNamespaceCommand : BaseCommand
    {

        public SetNamespaceCommand() : base(CommandResources.setnamespace) { }

        public new void Execute(FigaroContext context, string args)
        {
            base.Execute(context, args);
            ValidateArgCount(2, 2);
            context.SetNamespace(argv[0], argv[1]);
        }
    }

    public class SetProjectionCommand : BaseCommand
    {
        public SetProjectionCommand() : base(CommandResources.setprojection) { }

        public new void Execute(FigaroContext context, string args)
        {
            try
            {
                if (args.ToLower().Equals("on"))
                    context.SetDocumentProjection(true);
                else if (args.ToLower().Equals("off"))
                    context.SetDocumentProjection(false);
                else
                {
                    throw new ValidationException("command only accepts 'on' or 'off' as a parameter.");
                }
                return;
            }
            catch (ValidationException ve)
            {
                Warn(ve.Message);
                WarnUsage();
                return;
            }
        }
    }

    public class SetQueryTimeoutCommand : BaseCommand
    {
        public SetQueryTimeoutCommand() : base(CommandResources.setquerytimeout) { }

        public new void Execute(FigaroContext context, string args)
        {
            context.SetQueryTimeout(uint.Parse(args));
        }
    }

    public class SetVariableCommand : BaseCommand
    {
        public SetVariableCommand() : base(CommandResources.setvariable) {}

        public new void Execute(FigaroContext context, string args)
        {
            base.Execute(context, args);
            ValidateArgCount(2,2);
            context.SetVariable(argv[0],argv[1]);
        }
    }

    public class SetVerboseCommand : BaseCommand
    {
        public SetVerboseCommand() : base(CommandResources.setverbose) { }

        public new void Execute(FigaroContext context, string args)
        {
            base.Execute(context, args);

        }
    }

    public class SetIgnoreCommand : BaseCommand
    {
        public SetIgnoreCommand() : base(CommandResources.setignore) { }

        public new void Execute(FigaroContext context, string args)
        {
            base.Execute(context, args);

            ValidateLiteral(argv[0],new[]{"true", "false", "on", "off"});

            if (argv[0].ToLower().Equals("true") || argv[0].ToLower().Equals("on"))
            {
                Ignore = true;
                return;
            }

            if (argv[0].ToLower().Equals("false") || argv[0].ToLower().Equals("off"))
            {
                Ignore = false;
                return;
            }

            return;
        }

        public bool Ignore { get; set; }
    }

    public class TransactionCommand : BaseCommand
    {
        public TransactionCommand() : base(CommandResources.transaction) { }

        public new void Execute(FigaroContext context, string args)
        {
            if (FigaroProductInfo.ProductEdition != FigaroProductEdition.TransactionalDataStore &&
                FigaroProductInfo.ProductEdition != FigaroProductEdition.HighAvailability)
            {
                Warn(DbxmlResources.MustUseTDS);
                throw new ValidationException();
            }

            context.BeginTransaction();
        }
    }

    public class UpgradeContainerCommand : BaseCommand
    {
        public UpgradeContainerCommand() : base(CommandResources.upgradecontainer) { }

        public new void Execute(FigaroContext context, string args)
        {
            base.Execute(context, args);
            ValidateArgCount(1, 1);
            context.UpgradeContainer(argv[0]);
            return;
        }
    }
}