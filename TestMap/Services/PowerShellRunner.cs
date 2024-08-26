using System.Management.Automation.Runspaces;

namespace TestMap.Services;

public class PowerShellRunner
{
    public List<string> Errors { get; private set; }
    public List<string> Output { get; private set; }
    
    public bool Error { get; private set; }

    public async Task RunScript(List<string> commands)
    {
        // Create a runspace
        using (var runspace = RunspaceFactory.CreateRunspace())
        {
            runspace.Open();

            // Create a pipeline within the runspace
            using (var pipeline = runspace.CreatePipeline())
            {
                // Add the script text to the pipeline
                foreach (var command in commands)
                {
                    pipeline.Commands.AddScript(command);
                }

                // Add the output stream to capture standard output
                pipeline.Commands.Add("Out-String");

                // Execute the script
                var results = await Task.Run(() => pipeline.Invoke());

                // Process the output
                foreach (var result in results)
                {
                    string output = result.BaseObject.ToString();
                    if (!string.IsNullOrEmpty(output))
                    {
                        Output.Add(output);
                    }
                }

                // Check for errors
                var errors = pipeline.Error.ReadToEnd();
                if (errors != null && errors.Count > 0)
                {
                    foreach (var error in errors)
                    {
                        string errorString = error.ToString();
                        // Check if error is actually an error or just part of normal output
                        if (errorString != null && IsError(errorString))
                        {
                            Errors.Add(errorString);
                            Error = true;
                        }
                        else
                        {
                            Output.Add(errorString ?? string.Empty); // treat as output
                        }
                    }
                }
            }
        } 
    }

    private bool IsError(string line)
    {
        // Customize this method based on how errors are identified in your environment
        // Example: check for keywords that indicate errors
        return line.Contains("Error:") || line.StartsWith("fatal:");
    }

    public PowerShellRunner()
    {
        Errors = new List<string>();
        Output = new List<string>();
    }
}