# Research Checklist
From SIGSOFT Empirical SE Standards.

Paul Ralph et al. (2021) Empirical Standards for Software Engineering Research. arXiv:2010.03525 [cs.SE].
## General
### Specific Attributes
#### Essential Attributes
- states a purpose, problem, objective, or research question
- explains why the purpose, problem, etc. is important (motivation)
- defines jargon, acronyms and key concepts

- names the methodology or methodologies used
- methodology is appropriate (not necessarily optimal) for stated purpose, problem, etc.
- describes in detail what, where, when and how data were collected (see the Sampling Supplement)
- describes in detail how the data were analyzed

- presents results
- results directly address research questions
- enumerates and validates assumptions of statistical tests used (if any)1

- discusses implications of the results
- discloses all major limitations
- states clear conclusions which are linked to research question (or purpose, etc.) and supported by explicit evidence (data/observations) or arguments
- contributes in some way to the collective body of knowledge
- language is not misleading; any grammatical problems do not substantially hinder understanding
- acknowledges and mitigates potential risks, harms, burdens or unintended consequences of the research (see the ethics supplements for Engineering Research, Human Participants, or Secondary Data)
- visualizations/graphs are not misleading (see the Information Visualization Supplement)
- complies with all applicable empirical standards

#### Desirable Attributes
- states epistemological stance
- summarizes and synthesizes a reasonable selection of related work (not every single relevant study)
- clearly describes relationship between contribution(s) and related work
- demonstrates appropriate statistical power (for quantitative work) or saturation (for qualitative work)
- describes reasonable attempts to investigate or mitigate limitations
- discusses study’s realism, assumptions and sensitivity of the results to its realism/assumptions
- provides plausibly useful interpretations or recommendations for practice, education or research
- concise, precise, well-organized and easy-to-read presentation
- visualizations (e.g. graphs, diagrams, tables) advance the paper’s arguments or contribution
- clarifies the roles and responsibilities of the researchers (i.e. who did what?)
- provides an auto-reflection or assessment of the authors’ own work (e.g. lessons learned)
- publishes the study in two phases: a plan and the results of executing the plan (see the Registered Reports Supplement)
- uses multiple raters, where philosophically appropriate, for making subjective judgments (see the IRR/IRA Supplement)

#### Extraordinary Attributes
- applies two or more data collection or analysis strategies to the same research question (see the Multimethodology Standard)
- approaches the same research question(s) from multiple epistemological perspectives
- innovates on research methodology while completing an empirical study

## Data Science

### Specific Attributes
####  Essential Attributes
- explains why it is timely to investigate the proposed problem using the proposed method
- explains how and why the data was selected
- presents the experimental setup (e.g. using a dataflow diagram)2
- describes the feature engineering approaches3 and transformations that were applied
- explains how the data was pre-processed, filtered, and categorized
- EITHER: discusses state-of-art baselines (and their strengths, weaknesses and limitations) OR: explains why no state-of-art baselines exist OR: provides compelling argument that direct comparisons are impractical
- defines the modeling approach(es) used (e.g. clustering then decision tree learning), typically using pseudocode
- discusses the hardware and software infrastructure used4
- justifies all statistics and (automated or manual) heuristics used
- describes and justifies the evaluation metrics used

- goes beyond single-dimensional summaries of performance (e.g., average; median) to include measures of variation, confidence, or other distributional information

- discusses technical assumptions and threats to validity that are specific to data science5

#### Desirable Attributes
- provides a replication package including source code and data set(s), or if data cannot be shared, synthetic data to illustrate the use of the algorithms6
- data is processed by multiple learners, of different types7
- data is processed multiple times with different, randomly selected, training/test examples; the results of which are compared via significance tests and effect size tests (e.g. cross-validation)
- carefully selects the hyperparameters that control the data miners (e.g. via analysis of settings in related work or some automatic hyperparameter optimizer such as grid search)
- manually inspects some non-trivial portion of the data (i.e. data sanity checks)
- clearly distinguishes evidence-based results from interpretations and speculation8

#### Extraordinary Attributes
- leverages temporal data via longitudinal analyses (see the Longitudinal Studies Standard)
- triangulates with qualitative data analysis of selected samples of the data
- triangulates with other data sources, such as surveys or interviews
- shares findings with and solicits feedback from the creators of the (software) artifacts being studied

## Repository Mining

### Specific Attributes
#### Essential Attributes
- explains why repository mining is appropriate for the proposed research problem

- defines unit(s) of analysis or observation
- describes and justifies the data sources (e.g. GitHub, StackOverflow)
(if the selected data source(s) are obscure) explains in detail why they are appropriate for the goals of the study (e.g. consider number of repositories data quality, terms and conditions that may limit access to information)
- describes and justifies how the repositories are selected from the data sources (e.g. selection criteria, use of third-party tools, APIs, programming languages of choice)
- describes how the inclusion and exclusion criteria were validated (e.g., by manually inspecting the results of the automated search)
- describes the selected repositories
- describes the procedure for the acquisition (e.g., first selecting repositories and then downloading, or all completed together)
- if the data obtained is too large to be processed in its entirety
  - explains why (e.g., unfeasibility of a manual study, processing limitations, scope limitations)
  - explains the sampling strategy (see the Sampling Supplement)
- describes dataset characteristics including size of the selected repositories, and dataset attributes relevant to the study at hand (e.g., number of commit messages)
- describes data preprocessing steps
- if manual annotations are carried out:
  - uses multiple annotators; reports the number of annotators
  - describes the annotators (e.g. demographics, experience, training),
  - describes in detail the annotation procedure (e.g. what types of questions were asked to the annotators),
  - assesses inter-rater reliability (see the Inter-Rater Reliability Supplement)
- describes and justifies measures or metrics used
- EITHER: uses previously validated measures, OR: assesses construct validity

- if predictive modeling is used, complies with the Data Science Standard

- discusses threats to external validity (e.g. caused by selection of data source(s) and repositories, selection criteria, search strings)
#### Desirable Attributes
- provides supplemental materials (e.g. complete dataset, tool(s) used to download, select, pre-process, and post-process the selected repositories)
- uses probability sampling (see the Sampling Supplement)
- suggests future work that validates or uses the same sample
- quantitatively assess construct validity (e.g. using factor analysis)
- triangulates across data sources, informants or researchers
- annotators reflect on their own possible biases
- qualitative analysis of scenarios where the data collection or analysis tools were ineffective
- performs testing (e.g., unit testing) to avoid bugs in the proposed tool
- builds, tests or extends theory
- tests formal hypotheses
- discusses ethical issues in mining of software repositories1 (e.g., data privacy)


#### Extraordinary Attributes
- establishes causality, e.g. using longitidunal analyses