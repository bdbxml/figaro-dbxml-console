# figaro-dbxml-shell
The dbxml shell is instrumental for Oracle Berkeley DB XML (OBDBXML) developers allowing users to run the most common administrative and maintenance tasks, such as creating containers and querying content.

This version of the dbxml utility has been re-written as a .NET console application using the Figaro library at [http://bdbxml.net]. It is not guaranteed to be 100% compatible with the original dbxml utility that comes with Oracle Berkeley DB XML; in fact, several opportunities were taken to make it easier to use and more feature rich. The original help documentation for this shell (as well as the Figaro library itself) can be found at [http://help.bdbxml.net/html/1a2e0897-5978-4803-b8f2-d2891b24397e.htm].

<Please test and verify any scripts made with the original version before using in a production environment.

The dbxml utility provides an interactive shell that you can use to manipulate containers, documents and indices, and to perform XQuery queries against those containers.

dbxml uses an optional Berkeley DB home (environment) directory, which defaults to the current directory. An attempt is made to join an existing environment; if that fails, a private environment is created in the specified location. dbxml has a concept of a default open container; that is, the container upon which container operations such as adding and deleting indices are performed. The default container is set by use of the createContainer and openContainer commands. An in-memory container can be created using the command, `createContainer ""`. This is useful for using dbxml without file system side effects.

For a list of the commands available in the shell, use the `help` command. For help on a specific command, pass the command's name to the `help` command. For example:

```
dbxml> help createContainer
```

#Shell Initialization Commands
```
dbxml [-c] [-h homeDirectory]  [-P password] [-s script] [-t] [-V] [-v]  [-x] [-z] [-?]
```
##Parameters
`-c`

Create a new environment in the directory specified by the `-h` option. This option should only be used for debugging, since it does not allow you to specify important environment configuration options.

`-h <homeDirectory>`

Specify a home directory for the database environment; by default, the current working directory is used.

`-P <password>`

Specify an environment password. Although BDB utilities overwrite password strings as soon as possible, be aware there may be a window of vulnerability on systems where unprivileged users can see command-line arguments or where utilities are not able to overwrite the memory containing the command-line arguments.

`-s <script>`

Execute the dbxml commands contained in the script file upon shell startup. The commands must be specified one to a line in the script file. If any of the commands contained in the script file fail, the shell will not start.
For example, the following is the contents of a script that creates a container, loads several files into it, performs a query, and then prints the results:
```
dbxml> createContainer myContainer.dbxml
dbxml> putDocument a {<a><b name="doc1">doc1 n1</b><c>doc1 n2</c></a>}
dbxml> putDocument Avocado D:\dev\data\xmlData\nsData\Avocado.xml f
dbxml> putDocument a {<a><b name="doc3">doc3 n1</b><c>doc3 n2</c></a>}
dbxml> query collection("myContainer.dbxml")/a/b
dbxml> print
```
>When using the putDocument command, be sure to use forward slashes ('/') in your paths, and not the backslash ('\'), or you may get a streaming error.

If you are using dbxml to manipulate containers that are managed by an existing database environment, you must specify the path to that existing database environment. dbxml cannot be used to create environment files that can be shared with other applications. It will either create a private environment, or join an existing, shareable environment created by another application.

`-t`

Transaction mode. Transactions can be used, and are required for writes.

`-V`

Print software version.

`-v`

Verbose option. Specifying this option twice will increase the verbosity output.

`-x`

Secure mode - disallow access to the local file system and the Internet.

`-z <size>`

If an environment is created, set the cache size to <size> Mb (default: 64)

`-?`

Print the help message.

#Environment Variables
`DB_HOME`

If the `-h` option is not specified and the environment variable `DB_HOME` is set, it is used as the path of the database home.

#Interactive Shell Commands
## Hashtag character(#)
Comment specifier. Does nothing.
```
dbxml> #this does nothing
```
##addAlias
Add an alias to the default container. You can refer to this alias, instead of the container's name, when referencing the container in queries.
Usage: `addAlias <alias>`

Parameter | Description
--- | ---
alias | The alias to use for the container.

Use openContainer for opening the default container.
```
dbxml> opencontainer clients.dbxml
dbxml> addalias clients
Added alias: clients
```
##addIndex
Add an index to the container.

If the namespaceUri and name are not set, then this command adds to the default index.

Usage: `addIndex [<nodeNamespaceUri> <nodeName>] <indexDescription>`

Parameter | Description
--- | ---
nodeNamespaceUri | Optional. The namespace URI of the indexed node or attribute. You can specify using the default namespace by entering `""` for this value.
nodeName | Optional. The name of the node or attribute to be indexed.
indexDescription | The index, in `[unique]-{path type}-{node type}-{key type}-{syntax type}` format. See the help documentation for more information on indices.
```
dbxml> addIndex "" Key unique-node-element-equality-string
Adding index type: unique-node-element-equality-string to node: {}:Key
```

##commit
Commits the current transaction in a transactional dbxml shell environment, and starts a new one.

Usage: `commit`

##close
Closes a container opened by the dbxml shell.

Usage: `close <containerName>`

Parameter | Description
--- | ---
`[containerName>]` | Optional. The container to close. If a container isn't specified, all open containers are closed.
```
dbxml> close testdb.dbxml
Container testdb.dbxml closed.
You have 2 containers open.
dbxml>
```
>As shown in the above example, if you have multiple containers open you will get a notification of how many containers you have remaining open.

##compactContainer
Compacts a container to shrink its size.

Usage: `openContainer <containerName>`

Parameter | Description
--- | ---
`<containerName>` | The container to compact.
```
dbxml> compactContainer testdb.dbxml
Container compacted: testdb.dbxml
```
##contextQuery
Execute the query expression using the last results as the context item.

Usage: `contextQuery <queryExpression>`

Parameter | Description
--- | ---
`<queryExpression>` | The query to run against the current results.
```
dbxml> getDocuments
4 documents found
dbxml> contextQuery "let $doc:= . where $doc/node1=7 return $doc"
1 objects returned for expression  'let $doc:= . where $doc/node1=7 return $doc'
```
##cquery
Execute the query expression in the context of the default (current) container.

Usage: `cquery <queryExpression>`

Parameter | Description
--- | ---
`<queryExpression>` | The query to run against the container. This is useful for scenarios where you don't want to explicitly use the `collection()` function in your query.
```
dbxml> opencontainer shelltest.dbxml
dbxml> setnamespace "" "http://schemas.endpoint-systems.net/samples/figaro/v1/"
Binding -> http://schemas.endpoint-systems.net/samples/figaro/v1/
dbxml> cquery "for $f in /StoredTableData/Table return $f"
48 objects returned for eager expression 'for $f in /StoredTableData/Table return $f'
```
##createContainer
Creates a new container, which then becomes the new container.

Usage: `createContainer <containerName> [n|in|d|id] [[no]validate]`

If another container is open before this command is run, the container will be closed.

The default is to create a node storage container, with node indexes.

A container name of `""` creates an in-memory container.

Parameter | Description
--- | ---
`<containerName>` | The name of the container to create.
`n` | Creates a node storage container.
`in` | Creates a node storage container with node indexes.
`d` | Creates a document storage container.
`id` | Creates a document storage container with node indexes.
`[[no]validate]` | Create the container with Xml validation support.

```
createContainer edi834.dbxml d novalidate
```

##delIndex
Deletes an index from the default container.

Usage: `delIndex [<nodeNamespaceUri> <nodeName>] <indexDescription>`

If the namespaceUri and nodeName are not set, then this command deletes from the default index.

>You cannot delete the default node index in a container.

Parameter | Description
--- | ---
`nodeNamespaceUri` | The namespace of the deleted index's node.
`nodeName` | The name of the deleted index's node.
`indexDescription` | The indexing strategy, in `unique-node-metadata-equality-string` format.
```
dbxml> delindex http://schemas.endpoint-systems.net/samples/figaro/v1/ id unique-node-element-equality-string
Deleting index type: unique-node-element-equality-string from node: {http://schemas.endpoint-systems.net/samples/figaro/v1/}:id
dbxml>
```
##echo
This command echoes the (optional) text, followed by a newline.

Usage: `echo [text]`
```
dbxml> echo "hi there"
hi there
dbxml>
```
##getDocuments
Gets document(s) by name from the default container

Usage: `getDocuments [<docName>]`

If `docName` is set, it is looked up in the default container.

If no arguments are used, all documents in the container are looked up, and placed in the results.

The resulting document names and/or content can be displayed using the `print` command.

Parameter | Description
--- | ---
`<container>` | The container to open.
`[[no]validate]` | Open the container with Xml validation support.
```
dbxml> getdocuments instance1
1 documents found
```
##getMetaData
Get metadata item from the named content.

Usage: `getMetaData <docName> [<metaDataUri> <metaDataName>]`

Get a metadata item or a list of named metadata items from the named document. This method resets the default results to the returned value. This command, when used to return a specific item, is equivalent to the query expression:
```
                    for $i in doc('containerName/docName') return dbxml:metadata('metaDataUri:metaDataName', $i)
```                    
Parameter | Description
--- | ---
`<docName>` | The name of the document to retrieve metadata for.
`metaDataUri` | (Optional) The URI of the referenced metadata.
`metaDataName` | The name of the referenced metadata to look up.
```
dbxml> getmetadata instance1
Metadata for document: instance1 http://www.sleepycat.com/2002/dbxml:name
dbxml>
```
##help
Print help information. Use `help <commandName>` for extended help.

##info
Get info on the default container.

Usage:  `info [preload]`

Parameter | Description
--- | ---
`preload` | If specified, return information on any preloaded containers.
```
dbxml> info
Version: Oracle: Berkeley DB XML 2.4.16: (October 21, 2008)
Berkeley DB 4.6.21: (September 27, 2007)
Default container name: shelltest.dbxml
Type of default container: NodeContainer
Index Nodes: on Shell and XmlManager
state: Not transactional
Verbose: on Query context
state: LiveValues,Eager
```
##listIndexes
List all indexes in the default container.

Usage: `listIndexes`
```
dbxml> listindexes
Index: unique-node-metadata-equality-string for node {http://www.sleepycat.com/2002/dbxml}:name
1 indexes found.
dbxml>
```
##lookupEdgeIndex
Performs an edge lookup in the default container.

Usage: `lookupEdgeIndex <indexDescription> <namespaceUri> <nodeName> <parentNamespaceUri> <parentNodeName> [[<operation>] <value>]`

Parameter | Description
--- | ---
`<indexDescription>` | The indexing strategy, in `unique-node-metadata-equality-string` format.
`<namespaceUri>` | The URI of the referenced node.
`<nodeName>`|The name of the referenced node.
`<parentNamespaceUri>`|The URI of the parent of the referenced node.
`<parentNodeName>`|The name of the parent of the referenced node.
`<operation>`|Valid operations are '>', '<', '>=', '<=', or the default value of '='.
`<value>`|The name of the parent of the referenced node.

##lookupIndex
Performs an index lookup in the default container.

Usage: `lookupIndex <indexDescription> <namespaceUri> <nodeName> [[<operation>] <value>]`

Parameter | Description
---|---
`<indexDescription>`|The indexing strategy, in `unique-node-metadata-equality-string` format.
`<namespaceUri>`|The URI of the referenced node.
`<nodeName>`|The name of the referenced node.
`<operation>`|Valid operations are '>', '<', '>=', '<=', or the default value of '='.
`<value>`|The name of the parent of the referenced node.

Valid operations are '<', '<=', '>', '>=' and '=', and the default operation is '='. Available indexes can be found using the listIndexes command.
```
dbxml> listindexes
Index: unique-node-metadata-equality-string for node {http://www.sleepycat.com/2002/dbxml}:name
1 indexes found.
dbxml> lookupIndex node-metadata-equality-string http://www.sleepycat.com/2002/dbxml name avocado
1 objects returned for eager index lookup 'node-metadata-equality-string'
dbxml>
```

##lookupStats
Look up statistics on the default container.

Usage: `lookupStats <indexDescription> <namespaceUri> <nodeName> [<parentNamespaceUri> <parentNodeName> <value>]`

Parameter|Description
---|---
`<indexDescription>`|The indexing strategy, in unique-node-metadata-equality-string format.
`<namespaceUri>`|The URI of the referenced node.
`<nodeName>`|The name of the referenced node.
`<parentNamespaceUri>`|The URI of the parent of the referenced node.
`<parentNodeName>`|The name of the parent of the referenced node.
`<operation>`| Valid operations are '>', '<', '>=', '<=', or the default value of '='.
`<value>`|The name of the parent of the referenced node.

The optional parent URI and name are used for edge indexes.

Available indexes can be found using the listIndexes command.
```
dbxml> lookupStats node-metadata-equality-string http://www.sleepycat.com/2002/dbxml name
Number of  Indexed Keys: 1 Number of Unique Keys: 1 Sum Key Value Size: 12
dbxml>
```
##openContainer
Open a container, and use it as the default container.

Usage: `openContainer <container> [[no]validate]`

Parameter |Description
---|---
`<container>`|The container to open.
`[[no]validate]`|Open the container with Xml validation support.
```
dbxml> openContainer clients.dbxml validate
```
##preload
Preloads (opens) a container.

Usage: `preload <container>`

This command calls the OpenContainer method to open the container and store the resulting object in a vector. This holds the container open for the lifetime of the program. There is no corresponding unload or close command.

Paramete|Description
---|---
`<container>`|The container to preload (open).
```
dbxml> preload test2.dbxml
dbxml>
```
##prepare
Prepare the given query expression as the default pre-parsed query.

Usage: `prepare <queryExpression> [[no]validate]`

Parameter|Description
---|---
`<queryExpression>`|The query to prepare.
```
dbxml> prepare "for $veg in collection('shelltest.dbxml')/vegetables:item return $veg"
Prepared expression 'for $veg in collection('shelltest.dbxml')/vegetables:item return $veg'
```
##print/printNames
Prints most recent results, optionally to a file

Usage: `print | printNames [n Number] [pathToFile]`
If `printNames` is used, the results are turned into document names and printed if possible. If the results cannot be converted, the command will fail. If the optional argument `n` is specified followed by a number, then only the specified number of results are printed. If the optional `pathToFile` parameter is specified, the output is written to the named file rather than `stdout`.

##putDocument
Insert a document into the default container.

Usage: ` putDocument <nameprefix> <string> [f|s|q]`

Insert a document one of three ways:
- By string content (specify `s`)
- By filename. String is a filename, specify `f`
- By XQuery. String is an XQuery expression, specify `q`

##putDocuments
Put a collection of documents found in the specified directory, with the optional file filter, into the default container.
>This command incorporates the files names, without the file extensions, from the file system when inserting into the container. If a file already exists with the same name in the container, an exception will be thrown and processing will stop.

Usage: `putDocuments <directory> [filter]`

Parameter | Description
---|---
`directory`|The directory containing the XML files you wish to put into the default container.
`[file filter]`|The file filter. The default value is ' *.xml '.

In this example we're inserting XML files with the default file filter into the default container:
```
dbxml>putdocuments C:\dev\db\xmldata\simpledata\
298 documents inserted into groceries.dbxml container.
dbxml>
```
##query
Execute the given query expression, or the default pre-parsed query.

Usage: `query [queryExpression]`

Parameter|Description
---|---
`[queryExpression]`|(Optional) query expression to execute. If you previously prepared a query, you do not have to enter an expression.

In this example we are executing the query we prepared in the prepare command:
```
dbxml> query
100 objects returned for eager expression 'for $veg in collection('shelltest.dbxml')/vegetables:item return $veg'
dbxml>
```
##queryPlan
Prints the query plan for the specified query expression.

Usage: `queryPlan <queryExpression> [pathToFile]`

Parameter|Description
---|---
`<queryExpression>`|The query expression to evaluate the query plan for.
`[pathToFile]`|The optional file path to save the query plan to.
```
dbxml> queryPlan "for $veg in collection('shelltest.dbxml')/vegetables:item return $veg"
<XQuery> <Return> <ForTuple uri="" name="veg"> <ContextTuple/> <QueryPlanToAST> <StepQP axis="child" prefix="vegetables" uri="http://groceryItem.dbxml/vegetables" name="item" nodeType="element"> <SequentialScanQP container="shelltest.dbxml" nodeType="document"/> </StepQP> </QueryPlanToAST> </ForTuple> <QueryPlanToAST> <VariableQP name="veg"/> </QueryPlanToAST> </Return> </XQuery>
dbxml>
```
##quit
Quits the dbxml shell.

Usage: `quit`

##reindexContainer
Re-index a container, optionally changing index type.

Usage: `reindexContainer <containerName> <d|n>`

This command can take a long time on large containers.

Containers must be closed, and should be backed up before executing this command.

Parameter|Description
---|---
`<containerName>`|The container to re-index. The container must be closed before running this command.
`<d|n>`|Change the indexing type from nodes ('`n`') to documents ('`d`'), or vice versa, if required.
```
dbxml> reindexcontainer shelltest.dbxml
Container reindexed: shelltest.dbxml
dbxml>
```
##removeAlias
Remove an alias from the default container.

Usage: `removeAlias <alias>`

Parameter|Description
---|---
`<alias>`|The alias associated with the default container.
```
dbxml> opencontainer shelltest.dbxml
dbxml> addalias 'shelltest'
Alias 'shelltest' added to container 'shelltest.dbxml'.
dbxml> removealias 'shelltest'
Removed alias 'shelltest' from container 'shelltest.dbxml'.

dbxml>
```
##removeContainer
Removes (deletes) the named container.

Usage: `removeContainer <containerName>`

Removes the named container. The container must not be open, or the command will fail. If the container is the current container, the current results and container are released in order to perform the operation.

Parameter|Description
---|---
`<container>`|The container to remove.
```
dbxml> removeContainer test2.dbxml
Removing container: test2.dbxml
Container removed
dbxml>
```
##removeDocument
Remove a document from the default container.

Usage: `removeDocument <docName>`

>Document names are used in both node and document container types.

Parameter|Description
---|---
`<docName>`|The name of the document to remove.
```
dbxml> removedocument Artichoke
Document deleted, name = Artichoke

dbxml>
```
##run
Runs the given file as a `dbxml` script.

Usage: `run <scriptFile>`
Parameter|Description
---|---
`<scriptFile>`|The file to execute.

##setAutoIndexing
Set auto-indexing state of the default container.

Usage: `setAutoIndexing <on|off>`

Sets the auto-indexing state of the specified value. The info command returns the current state of auto-indexing.

##setBaseUri
Get/set the base URI in the default context.

Usage: `setBaseUri [<uri>]`

This command calls the BaseUri property.

Parameter|Description
---|---
`[<uri>]`|The base URI. Must be in the form "scheme:path". If not specified, the command returns the current base URI.
```
dbxml> setbaseuri http://groceryItem.dbxml/fruits
Base URI = 'http://groceryItem.dbxml/fruits'
dbxml> setBaseUri
Current base URI: 'http://groceryItem.dbxml/fruits'

dbxml>
```
##setIgnore
Tell the shell to ignore script errors.

Usage:` setIgnore <on|off>`

When set `on`, errors from commands in dbxml shell scripts will be ignored. When off, they will cause termination of the script. Default value is `off`.

##setLazy
Sets lazy evaluation on or off in the default context.

Usage: `setLazy <on|off>`

##setMetaData
Set a metadata item on the named document.

Usage: `setMetaData <docName> <metaDataUri> <metaDataName> <metaDataType> <metaDataValue>`

Parameter|Description
---|---
`<docName>`|The name of the document to add metadata to.
`<metaDataUri>`|The URI of the metadata.
`<metaDataName>`|The name of the metadata.
`<metaDataValue>`|The value of the metadata.
```
dbxml> setMetadata apples "http://fruits.net/" fruit Apple
MetaData item 'http://fruits.net/:fruit' added to document apples

dbxml> getmetadata apples
Metadata for document: apples
http://www.sleepycat.com/2002/dbxml:name
http://fruits.net/:fruit

dbxml>
```
##setNamespace
Create a prefix->namespace binding in the default context.

Usage: `setNamespace <prefix> <namespace>`
Parameter|Description
---|---
`<prefix>`|The namespace prefix used by the XML documents in the container.
`<namespace>`|The namespace associated with the specified prefix.
```
dbxml> setnamespace fruits http://groceryItem.dbxml/fruits
Binding fruits -> http://groceryItem.dbxml/fruits

dbxml>
```
##setProjection
Enables or disables the use of the document projection optimization.

Usage: `setProjection <on|off>`

>Document projection uses static analysis of the query to materialize only those portions of the document relevant to the query, which can significantly enhance performance of queries against documents from Wholedoc containers and documents not in a container.
>It should not be used if arbitrary navigation of the resulting nodes is to be performed, as not all nodes in the original document will be present and unexpected results could be returned.

##setQueryTimeout
Set a query timeout in seconds in the default context.

Usage: `setQueryTimeout <seconds>`

Parameter|Description
---|---
`<seconds>`|The query timeout, specified in seconds.
```
dbxml> setQueryTimeout 10
Setting query timeout to 10 seconds
dbxml>
```
##setVariable
Sets an untyped variable in the current context.

Usage: `setVariable <varName> <value> [(<value> ...)]`

Parameter|Description
---|---
`<varName>`|The variable name.
`<value>`|The variable value.
```
dbxml> setvariable fruit apple
Setting $fruit = apple

dbxml>
```
##setVerbose
set verbosity output settings for the shell.

Usage: `setVerbose <level> <category>`

Level is used for `SetLogLevel`.
Category is used for `SetCategory`.

Using `0 0` for the parameters turns verbosity off. Using -1 -1 turns on maximum verbosity.

The values are masks from the library, and can be combined. For example, to turn on INDEXER and optimizer messages, use a category of 0x03.

Parameter|Description
---|---
`<level>`| 
  |0x01 -- LEVEL_DEBUG -- program execution tracing
  |0x02 -- LEVEL_INFO -- informational messages
  |0x04 -- LEVEL_WARNING -- recoverable warnings
  |0x08 -- LEVEL_ERROR -- unrecoverable errors
  |-1 -- LEVEL_ALL --everything
`<category>`|
  |0x01 -- CATEGORY_INDEXER -- messages from the indexer
  |0x02 -- CATEGORY_QUERY -- messages from the query processor
  |0x04 -- CATEGORY_OPTIMIZER -- messages from the query optimizer
  |0x08 -- CATEGORY_DICTIONARY -- messages from the name dictionary
  |0x10 -- CATEGORY_CONTAINER -- messages from container management
  |0x20 -- CATEGORY_NODESTORE -- messages from node storage management
  |0x40 -- CATEGORY_MANAGER -- messages from the manager
  |-1 -- CATEGORY_ALL -- everything
```
#set verbose debug & warning levels on the indexer and container categories
dbxml> setverbose 0x01|0x04 0x01|0x10
dbxml>
```
##sync
Sync the current container to disk.

Usage: `sync`

This command syncs the current container to disk.

##time

Wrap a shell command in a wall-clock timer.

>The `echo`, `help`, and `setIgnore` commands do not respond to the time command.

If the verbose switch is enabled for the shell, the `time` command output is always generated.

Usage: `time <command>`

This command wraps a timer around the specified command and times its execution. The result is sent to the console window (stdout).
```
dbxml> time cquery "for $i in /fruits:item return $i"
96 objects returned for eager expression 'for $i in /fruits:item return $i'
Time in seconds for command 'cquery': 0.013
dbxml>
```

##transaction
Create a transaction for all subsequent operations to use.

Usage: `transaction`

Any transaction already in force is committed.

The following example is used after starting the dbxml shell with the "-t" option and after opening the default container:
```
dbxml> transaction
Transaction started
dbxml> putdocument apple1 d:\dev\db\xmlData\nsData\apples.xml f
Document added, name = apple1
dbxml> commit
Transaction committed
dbxml>
```
##upgradeContainer
Upgrade a container to the current container format.

Usage: `upgradeContainer <containerName>`

This command can take a long time on large containers. Containers should be backed up before running this command.
