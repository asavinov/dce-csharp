
     ____        _         ____                                     _
    |  _ \  __ _| |_ __ _ / ___| ___  __  __ __  __  __ _ __  _  __| |_ __ 
    | | | |/ _` | __/ _` | |    / _ \|  `´  |  `´  |/ _` |  \| |/ _  | '__/
    | |_| | (_| | || (_| | |___| (_| | |\/| | |\/| | (_| |   ' | (_| | |
    |____/ \__,_|\__\__,_|\____|\___/|_|  |_|_|  |_|\__,_|_|\__|\__,_|_|

	 I N T E G R A T E.          T R A N S F O R M.          A N A L Y Z E.

# DataCommandr: Integrate. Transform. Analyze. 

*DataCommandr* Engine is a C# library for data integration, transformation and analysis. DataCommandr is intended for defining (potentially highly complicated) *data mashups*, that is, new data that is derived from the input data by applying various transformations. In this sense, it pursues the same goal as conventional query languages (like SQL) and data processing frameworks (like MapReduce). The unique distinguishing feature is that DataCommander uses a novel *column-oriented* approach where new data columns are defined in terms of other data columns. It is opposed to most other languages and frameworks where new data tables are defined in terms of other data tables. In particular, it is an alternative to such conventional approaches as SQL-like languages and MapReduce. 

## Features 

DataCommander has the following distinguishing features: 

* DataCommandr is based on the [Concept-Oriented Model](http://conceptoriented.org) (COM) which is a unified model aimed at generalizing existing data models and data modeling techniques. 

* DataCommandr uses a novel concept-oriented expression language (COEL) for defining new tables and columns in terms of existing tables and columns. 

* DataCommandr uses a column-oriented approach where the main unit of data is a column and every column has its own definition in terms of other columns. Data processing is also performed column-wise rather than table-wise in most other systems. 

* DataCommandr uses an internal in-memory data processing engine which is optimized for fast computations. It loads data into memory and processes it column-wise which has higher performance for many data processing workloads.

* DataCommandr evaluates data in columns by using dependencies derived from column definitions. It is similar to the functional paradigm because the whole model is represented as a number of column and table definitions. 

* DataCommandr can be viewed as an analogue of classical spreadsheets where columns are defined in terms of other columns (in this or other tables) rather than cells being defined in terms of other cells. 

* It is important that three types of column definitions are used: 
  * **Calculate** columns - defining a new column depending on the values of other columns of the same table. It is a column-oriented analogue of select in SQL and map in MapReduce
  * **Link** columns - defining a column which references records from another table. It is a column-oriented analogue of joins.
  * **Aggregate** columns - defining a column which aggregates data from another table. It is a column-oriented analogue of group-by in SQL and reduce in MapReduce.

* There is a Java implementation of DataCommandr engine: http://bitbucket.org/conceptoriented/dc-core

* There exist a front-end for this library - a self-service tool for agile data transformations (WPF): http://bitbucket.org/conceptoriented/dc-wpf

## More Information

More information about DataCommandr and the underlying data model can be found here: 

* More information information on all aspects of concept-oriented paradigm including the concept-oriented model and concept-oriented programming including publications can found here: 
  * http://www.conceptoriented.org
  * http://www.conceptoriented.com

* Alexandr Savinov is an author of DataCommander Engine Java library as well as the underlying concept-oriented model (COM): 
  * http://conceptoriented.org/savinov
  * https://www.researchgate.net/profile/Alexandr_Savinov

* Some papers about this approach: 
  * A. Savinov. DataCommandr: Column-Oriented Data Integration, Transformation and Analysis. Proc. IoTBD 2016, 339-347. https://www.researchgate.net/publication/301764506_DataCommandr_Column-Oriented_Data_Integration_Transformation_and_Analysis
  * A. Savinov. ConceptMix: Self-Service Analytical Data Integration based on the Concept-Oriented Model. A. Savinov. 3rd International Conference on Data Technologies and Applications (DATA 2014), 78-84. https://www.researchgate.net/publication/265301356_ConceptMix_Self-Service_Analytical_Data_Integration_based_on_the_Concept-Oriented_Model
