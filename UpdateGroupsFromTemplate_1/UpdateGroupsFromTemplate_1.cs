namespace AutomationScript1_1
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Text;
	using System.Threading;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages;
	using Skyline.DataMiner.Net.Messages.Advanced;

	/// <summary>
	/// Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		/// <summary>
		/// The script entry point.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public void Run(IEngine engine)
		{
			try
			{
				RunSafe(engine);
			}
			catch (ScriptAbortException)
			{
				// Catch normal abort exceptions (engine.ExitFail or engine.ExitSuccess)
				throw; // Comment if it should be treated as a normal exit of the script.
			}
			catch (ScriptForceAbortException)
			{
				// Catch forced abort exceptions, caused via external maintenance messages.
				throw;
			}
			catch (ScriptTimeoutException)
			{
				// Catch timeout exceptions for when a script has been running for too long.
				throw;
			}
			catch (InteractiveUserDetachedException)
			{
				// Catch a user detaching from the interactive script by closing the window.
				// Only applicable for interactive scripts, can be removed for non-interactive scripts.
				throw;
			}
			catch (Exception e)
			{
				engine.ExitFail("Run|Something went wrong: " + e);
			}
		}

		private void RunSafe(IEngine engine)
		{
			var groupTemplate = engine.GetScriptParam("Group Template").Value;
			var groupsToEdit = engine.GetScriptParam("Groups to Edit").Value;

			var infoMessageResponse = engine.SendSLNetMessage(new GetInfoMessage(InfoType.SecurityInfo));

			var dmaGroups = infoMessageResponse.OfType<GetUserInfoResponseMessage>().FirstOrDefault().Groups;

			var groupTemplateInfo = dmaGroups.FirstOrDefault(x => x.Name == groupTemplate);

			if (groupTemplateInfo == null)
			{
				engine.ExitFail($"Group template '{groupTemplate}' not found.");
			}

			var elementsToAdd = groupTemplateInfo.Elements.ToList();

			var viewsToAdd = groupTemplateInfo.Views.ToList();

			var groupsToEditList = groupsToEdit.Split(',').Select(x => x.Trim()).ToList();

			var listPsa = new List<SA>();

			foreach (var element in elementsToAdd)
			{
				var sa = new SA
				{
					Sa = new[] { $"{element.ViewID}", $"{element.DmaID}/{element.ElementID}", "FALSE", "TRUE" },
				};

				listPsa.Add(sa);
			}

			foreach (var view in viewsToAdd)
			{
				var sa = new SA
				{
					Sa = new[] { $"{view.ID}", "FALSE", "FALSE" },
				};

				listPsa.Add(sa);
			}

			var saObject = listPsa.ToArray();

			var addMessage = new SetDataMinerInfoMessage();
			addMessage.Psa2 = new PSA { Psa = saObject };
			addMessage.What = 24; // Add

			var failedGroups = new List<string>();
			foreach (var groupName in groupsToEditList)
			{
				var matchingGroup = dmaGroups.FirstOrDefault(x => x.Name == groupName);

				if (matchingGroup != null)
				{
					RemoveCurrentViews(engine, matchingGroup);
					Thread.Sleep(500);
					RemoveCurrentElements(engine,matchingGroup);
					Thread.Sleep(500);

					addMessage.IInfo1 = matchingGroup.ID;
					engine.SendSLNetMessage(addMessage);
					Thread.Sleep(500);
				}
				else
				{
					failedGroups.Add(groupName);
				}
			}

			if (failedGroups.Count > 0)
			{
				engine.ShowProgress($"The following groups were not found: {string.Join(", ", failedGroups)}");
			}
			else
			{
				engine.ShowProgress($"All groups were edit successfully.");
			}

			Thread.Sleep(3000);
		}

		private void RemoveCurrentViews(IEngine engine, DataMinerUserGroup matchingGroup)
		{
			var removeViewMessage = new SetDataMinerInfoMessage();

			var viewsIdToRemove = matchingGroup.Views.Select(x =>(uint)x.ID).ToArray();
			removeViewMessage.Uia2 = new UIA { Uia = viewsIdToRemove };
			removeViewMessage.What = 25; // Remove View
			removeViewMessage.IInfo1 = matchingGroup.ID;

			removeViewMessage.bInfo1 = Int32.MaxValue;
			removeViewMessage.bInfo2 = Int32.MaxValue;
			removeViewMessage.IInfo2 = Int32.MaxValue;

			engine.SendSLNetMessage(removeViewMessage);
		}

		private void RemoveCurrentElements(IEngine engine, DataMinerUserGroup matchingGroup)
		{
			var removeElementsMessage = new SetDataMinerInfoMessage();

			var viewsIdToRemove = matchingGroup.Elements.Select(x => $"{x.DmaID}/{x.ElementID}").ToArray();
			removeElementsMessage.Var2 = viewsIdToRemove;
			removeElementsMessage.What = 361; // Remove Element
			removeElementsMessage.Var1 = matchingGroup.ID;
			engine.SendSLNetMessage(removeElementsMessage);
		}
	}
}
