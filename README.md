**Overview of Change Hot Spot Locator (CHSL):**

CHSL is a TFS change history analysis tool. CHSL's basic idea is to calculate which methods have changed between two file versions.
Identification of changed methods works by comparing their corresponding abstract syntax trees (ASTs). Various TFS analysis options ranging from analysing a single changeset to
a self defined TFS query are provided.

**Dependencies:**
 - NRefactory 5.5.1
 - TFS 2010 API

**How to use**

Analysis of local files:
 - MethodComparer.exe oldFile newFile
 - Gui_Demo.exe, the content of the old/new file can be defined via a textBox.
	
Analysis of TFS Items:

Setup:

An IniFile with the TfsServerConfiguration parameters has to be provided. This could be managed by overwriting the default "config.ini" file, 
or by defining a new one and provide it via the commandline option -i=file. Have a look at the default IniFile in order to know which parameters should be defined.

Usages:
 - Changeset: CHSL.exe -c=changesetId
 - Workitem: CHSL.exe -w=workItemId
 - SharedQuery: CHSL.exe -q=path/queryname
 - Query: CHSL.exe -qstring="query"

After the successful execution a report file is written to "./report.csv". Further explanation can be obtained by CHSL.exe --help.


	
	
