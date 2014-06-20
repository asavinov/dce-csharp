Configure:
- Adding ANTLR (for generating grammars - could be done via Java library but does not work for all versions). This makes it possible to compile grammars during VS build (not manually)
  - Right click solution root
  - Choose Manage NuGet Packages
  - Type antlr4 in Search field
  - Ensure that Include Prerelease is chosen
  - Install "ANTLR 4 Runtime" or "ANTLR 4"
  - Choose projects where a reference to the DLL has to be added (it will be visible as a project Item in References)
  - the files will be installed in "packages" directory
- Support for ANTLR in Visual Studio (for syntax highlighting, creating new VS grammar project items etc.)
  - Tools | Extensions and Update
  - Click Online
  - Type antlr in Search field
  - Install "ANTLR Language Support"
  - the files will be installed in "packages" directory
- Git Extensions is installed by downloading (not necessary - only for Git)

Git for Visual Studio:
Git Source Control Provider (plug-in that integrates git with Visual Studio): http://gitscc.codeplex.com/
Visual Studio Tools for Git (Microsoft extension for Team Explorer): http://visualstudiogallery.msdn.microsoft.com/abafc7d6-dcaa-40f4-8a5e-d6724bdb980c
Git Extensions (Git intergration for Visual Studio): 

A list of various gitignore files: 
https://github.com/github/gitignore
VisualStudio.gitignore is used in comcsharp

Gigignore for Visual Studio: 
http://stackoverflow.com/questions/2143956/gitignore-for-visual-studio-projects-and-solutions

NuGet:
http://docs.nuget.org/docs/creating-packages/hosting-your-own-nuget-feeds


ANTLR
-----

C# target for ANTLR 4: https://github.com/sharwell/antlr4cs

Grammar articles:
http://www.codeproject.com/Articles/18880/State-of-the-Art-Expression-Evaluation

NCalc grammar (see also VS project configuration):
http://ncalc.codeplex.com/SourceControl/changeset/view/914d819f2865#Grammar/NCalc.g


steps & code to walk AST in C#: 
http://stackoverflow.com/questions/887205/tutorial-for-walking-antlr-asts-in-c

C# and Antlr: 
http://www.manuelabadia.com/blog/PermaLink,guid,6e509f70-3db8-46f4-b451-1ef9dd80740b.aspx

How do I make ANTLRWorks and Visual Studio work together? (CSharp as target language)
http://www.antlr.org/wiki/pages/viewpage.action?pageId=557075


