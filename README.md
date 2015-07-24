Overview of tfs_error_location:

Compares the methods of two files and detects which methods have changed based on syntaxtrees. 
Further it can be linked to a TFS server in order to analyze all method changes of changesets/workItems/queries.

**How to use**

Analysis of local files:
 - Tfs_Error_Location.exe without parameters: uses two example files
 - Tfs_Error_Location.exe oldFile newFile
 - Gui_Demo.exe, the content of the old/new file can be defined via a textBox.
	
Analysis of Team Foundation Server Items:
 - Changeset: TfsMethodChanges.exe -c=number
 - Workitem: TfsMethodChanges.exe -w=number
 - Query (Name): TfsMethodChanges.exe -qname=name

Further explanation can be obtained by TfsMethodChanges.exe --help.


	
	
