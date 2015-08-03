**Overview of tfs_error_location:**

Compares the methods of two files and detects which methods have changed based on syntaxtrees. 
Further it can be linked to a TFS server in order to analyze all method changes of changesets/workItems/queries.

**Dependencies:**
 - NRefactory 5.5.0
 - Mono.Cecil 0.9.5

**How to use**

Analysis of local files:
 - Tfs_Error_Location.exe without parameters: uses two example files
 - Tfs_Error_Location.exe oldFile newFile
 - Gui_Demo.exe, the content of the old/new file can be defined via a textBox.
	
Analysis of Team Foundation Server Items:

Setup:

An IniFile with the TfsServerConfiguration parameters has to be provided. This could be managed by overwriting the default "config.ini" file, 
or by defining a new one and provide it via the commandline option -i=file to the program. Have a look at the default IniFile in order to know which parameters should be defined.

Usages:
 - Changeset: TfsMethodChanges.exe -c=changesetId
 - Workitem: TfsMethodChanges.exe -w=workItemId
 - Query: TfsMethodChanges.exe -q=path/queryname

After the successful execution a report file is written to "./report.csv". Further explanation can be obtained by TfsMethodChanges.exe --help.


	
	
