java -cp ..\..\Reference\Antlr4\antlr4-csharp-4.0.1-SNAPSHOT-complete.jar;%CLASSPATH% org.antlr.v4.Tool -o . -no-listener -visitor -package Com.Query -Dlanguage=CSharp_v4_0 Expr.g4 %*
