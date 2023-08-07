using News.Library;
using System.Linq;
using System.Threading.Tasks;

namespace GitHubActionsNews
{
    public static class Verify
    {
        public static async Task Run()
        {
            // download all action-** files from the storage account
            var allActions = await Storage.SeparateDownloadAllFilesThatStartWith("Actions-");
            foreach (var actionFile in allActions)
            {
                // log the number of actions in the file and the filename
                Log.Message($"Found {actionFile.Actions.Count} actions in file [{actionFile.FileName}]");
            }

            // loop over each file and count the number of times an action is already in any of the other files
            var uniqueFinds = 0;
            const string allLetters = "abcdefghijklmnopqrstuvwxyz ";
            foreach (var actionFile in allActions)
            {
                var fileUniqueFinds = 0;
                var letterCombo = actionFile.FileName.Substring("Actions-".Length);
                foreach (var action in actionFile.Actions)
                {
                    var count = 0;
                    foreach (var otherActionFile in allActions)
                    {
                        if (otherActionFile.FileName != actionFile.FileName)
                        {
                            if (otherActionFile.Actions.Any(item => item.RepoUrl == action.RepoUrl))
                            {
                                count++;
                            }
                        }
                    }

                    if (count == 0)
                    {
                        uniqueFinds++;
                        // Log.Message($"Found [{count}] times action [{action.RepoUrl}] in other files");
                        fileUniqueFinds++;
                    }
                    else
                    {
                        // reset file unique
                        //fileUniqueFinds = 0;
                    }
                }
                // check how many letter combinations would be unique
                var sumComboDuplicates = 0;
                var sumComboUniques = 0;
                foreach (var letter in allLetters)
                {
                    var testCombo = letterCombo + letter;
                    var comboDuplicates = 0;
                    var comboUniques = 0;
                    foreach (var otherActionFile in allActions)
                    {
                        if (otherActionFile.FileName != actionFile.FileName)
                        {
                            if (otherActionFile.Actions.Any(item => item.Title.Contains(testCombo)))
                            {
                                comboDuplicates++;
                            }
                            else
                            {
                                comboUniques++;
                            }
                        }
                    }
                    Log.Message($"Test combo of [{testCombo}] would result in [{comboDuplicates}] duplicates and [{comboUniques}] uniques");
                    sumComboDuplicates += comboDuplicates;
                    sumComboUniques += comboUniques;
                }
                Log.Message($"Running with the combos above would yield [{sumComboDuplicates}] duplicates and [{sumComboUniques}] uniques");
                Log.Message($"Unique finds {fileUniqueFinds}/{actionFile.Actions.Count} for file {actionFile.FileName} | {letterCombo}");
            }
            Log.Message($"Found [{uniqueFinds}] unique actions");
        }
    }
}