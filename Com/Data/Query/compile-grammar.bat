rem Compile Expr.g4 by producing also visitor classes
rem C:\Programme\Java\jre7\bin\java -cp ..\..\Reference\Antlr4\antlr4-csharp-4.0.1-SNAPSHOT-complete.jar;%CLASSPATH% org.antlr.v4.Tool -o . -no-listener -visitor -package Com.Query -Dlanguage=CSharp_v4_0 Expr.g4 %*
java -cp ..\..\packages\Antlr4.4.2.2-alpha001\tools\antlr4-csharp-4.2.2-SNAPSHOT-complete.jar;%CLASSPATH% org.antlr.v4.Tool -o ..\obj\Debug -no-listener -visitor -package Com.Query -Dlanguage=CSharp_v4_0 Expr.g4 %*

rem Compile Script.g4 by producing also visitor classes
rem java -cp ..\..\Reference\Antlr4\antlr-4.3-complete.jar;%CLASSPATH% org.antlr.v4.Tool -o . -no-listener -visitor -package Com.Query -Dlanguage=CSharp_v4_0 Script.g4 %*
java -cp ..\..\packages\Antlr4.4.2.2-alpha001\tools\antlr4-csharp-4.2.2-SNAPSHOT-complete.jar;%CLASSPATH% org.antlr.v4.Tool -o ..\obj\Debug -no-listener -visitor -package Com.Query -Dlanguage=CSharp_v4_0 Script.g4 %*
