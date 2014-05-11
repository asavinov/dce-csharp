rem Compile Expr.g4 by producing also visitor classes
rem java -cp ..\..\Reference\Antlr4\antlr4-csharp-4.0.1-SNAPSHOT-complete.jar;%CLASSPATH% org.antlr.v4.Tool -o . -no-listener -visitor -package Com.Query -Dlanguage=CSharp_v4_0 Expr.g4 %*

rem Compile Script.g4 by producing also visitor classes
java -cp ..\..\Reference\Antlr4\antlr4-csharp-4.0.1-SNAPSHOT-complete.jar;%CLASSPATH% org.antlr.v4.Tool -o . -no-listener -visitor -package Com.Query -Dlanguage=CSharp_v4_0 Script.g4 %*
