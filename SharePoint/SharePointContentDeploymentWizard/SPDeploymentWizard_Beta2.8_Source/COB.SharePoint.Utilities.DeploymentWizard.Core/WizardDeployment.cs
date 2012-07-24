﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Reflection;
using System.Xml;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Deployment;

namespace COB.SharePoint.Utilities.DeploymentWizard.Core
{
    /// <summary>
    /// Specifies the type of operation.
    /// </summary>
    public enum DeploymentType
    {
        Import,
        Export
    }

    /// <summary>
    /// Provides core engine of Wizard functionality. 
    /// </summary>
    public class WizardDeployment
    {
        #region -- Public events to notify clients of --

        public event EventHandler<SPDeploymentEventArgs> ProgressUpdated;
        public event EventHandler<SPDeploymentEventArgs> Completed;
        public event EventHandler<SPDeploymentEventArgs> Started;
        public event EventHandler<SPObjectImportedEventArgs> ObjectImported;
        public event EventHandler<InvalidChangeTokenEventArgs> ValidChangeTokenNotFound;

        #endregion

        #region -- Private members --

        private TraceHelper trace = null;
        private TraceSwitch traceSwitch = new TraceSwitch("COB.SharePoint.Utilities.DeploymentWizard.Core",
                "Trace switch for ContentDeploymentWizard");

        private static TraceHelper traceStatic = null;
        private static TraceSwitch traceSwitchStatic = new TraceSwitch("COB.SharePoint.Utilities.DeploymentWizard.Core",
                "Trace switch for ContentDeploymentWizard");

        private readonly string f_csDEFAULT_PUBLISHING_ASSEMBLY_NAME = "Microsoft.SharePoint.Publishing,version=12.0.0.0,publicKeyToken=71e9bce111e9429c,culture=neutral";

        private List<SPObjectData> exportObjectDetails = null;

        #endregion

        #region -- Public properties --

        /// <summary>
        /// Represents the settings for the current import operation.
        /// </summary>
        public SPImportSettings ImportSettings
        {
            get;
            set;
        }
        
        /// <summary>
        /// Represents the settings for the current import operation.
        /// </summary>
        public SPExportSettings ExportSettings
        { 
            get;
            set;
        }

        /// <summary>
        /// Specifies the type of the current deployment
        /// </summary>
        public DeploymentType Type
        {
            get;
            set;
        }

        public static string ChangeTokenNotFoundMessage
        {
            get
            {
                return
                    "Unable to perform incremental export - the Wizard did not find a stored change token for the largest object " +
                    "you have selected to export. This could be because a full export for the site or web has not yet been performed using this version of the Wizard.\n\n" +
                    "A full export will be performed on this run.";
            }
        }

        public static string InvalidObjectsForIncrementalDeploymentMessage
        {
            get
            {
                return
                    "Unable to perform incremental export - the objects selected for export did not include a site or web object. Incremental " +
                    "deployments can only be performed for site or web objects.\n\n" +
                    "A full export will be performed on this run.";
            }
        }
    
        #endregion

        #region -- Constructor -- 

        /// <summary>
        /// Initialises this class with settings for an import or export operation.
        /// </summary>
        /// <param name="DeploymentXml">XML containing settings - this should be initially generated by using the 'Save settings' functionality in the UI.</param>
        /// <param name="DeploymentType">The type of operation to perform.</param>
        public WizardDeployment(XmlTextReader DeploymentXml, DeploymentType DeploymentType)
        {
            trace = new TraceHelper(this);
            Type = DeploymentType;

            if (DeploymentType == DeploymentType.Export)
            {
                WizardExportSettings weSettings = CollectExportSettings(DeploymentXml);
                ExportSettings = (SPExportSettings) weSettings.Settings;
            }
            if (DeploymentType == DeploymentType.Import)
            {
                WizardImportSettings wiSettings = CollectImportSettings(DeploymentXml);
                ImportSettings = (SPImportSettings) wiSettings.Settings;
            }
        }

        #endregion

        #region -- Import/export settings --

        public void ValidateSettings()
        {
            if (traceSwitch.TraceVerbose)
            {
                trace.TraceVerbose("ValidateSettings: Entered.");
            } 

            SPDeploymentSettings settings = null;

            if (Type == DeploymentType.Export)
            {
                settings = ExportSettings;
                if (traceSwitch.TraceInfo)
                {
                    trace.TraceInfo("ValidateSettings: Validating export settings.");
                }
            }
            if (Type == DeploymentType.Import)
            {
                settings = ImportSettings;
                if (traceSwitch.TraceInfo)
                {
                    trace.TraceInfo("ValidateSettings: Validating import settings.");
                }
            }

            try
            {
                settings.Validate();
                if (traceSwitch.TraceInfo)
                {
                    trace.TraceInfo("ValidateSettings: Settings validated successfully.");
                }
            }
            catch (Exception e)
            {
                if (traceSwitch.TraceWarning)
                {
                    trace.TraceWarning("ValidateSettings: Caught exception when validating settings. Throwing exception. " +
                        "Exception = '{0}'.", e);
                }

                throw;
            }

            if (traceSwitch.TraceVerbose)
            {
                trace.TraceVerbose("ValidateSettings: Leaving.");
            }
        }

        public static WizardOperationSettings CollectSettings(XmlTextReader xReader)
        {
            WizardOperationSettings settings = null;
            while (xReader.Read())
            {
                if (xReader.Name == "ImportSettings")
                {
                    settings = WizardDeployment.CollectImportSettings(xReader);
                }
                if (xReader.Name == "ExportSettings")
                {
                    settings = WizardDeployment.CollectExportSettings(xReader);
                }
            }

            return settings;
        }

        internal static WizardImportSettings CollectImportSettings(XmlTextReader importSettingsXml)
        {
            if (traceSwitchStatic.TraceVerbose)
            {
                traceStatic.TraceVerbose("CollectImportSettings: Entered CollectImportSettings().");
            }

            if (importSettingsXml.Name != "ImportSettings")
            {
                importSettingsXml.Read();
            }

            // create SPImportSettings object..
            SPImportSettings importSettings = new SPImportSettings();

            if (importSettingsXml.Name == "ImportSettings")
            {
                if (importSettingsXml.MoveToAttribute("SiteUrl"))
                {
                    importSettings.SiteUrl = importSettingsXml.Value;
                }
                if (importSettingsXml.MoveToAttribute("ImportWebUrl"))
                {
                    if (!string.IsNullOrEmpty(importSettingsXml.Value))
                    {
                        importSettings.WebUrl = importSettingsXml.Value;
                    }
                }
                if (importSettingsXml.MoveToAttribute("FileLocation"))
                {
                    importSettings.FileLocation = importSettingsXml.Value;
                }
                if (importSettingsXml.MoveToAttribute("BaseFileName"))
                {
                    importSettings.BaseFileName = importSettingsXml.Value;
                }
                if (importSettingsXml.MoveToAttribute("IncludeSecurity"))
                {
                    importSettings.IncludeSecurity = (SPIncludeSecurity)Enum.Parse(typeof(SPIncludeSecurity), importSettingsXml.Value);
                }
                if (importSettingsXml.MoveToAttribute("VersionOptions"))
                {
                    importSettings.UpdateVersions = (SPUpdateVersions)Enum.Parse(typeof(SPUpdateVersions), importSettingsXml.Value);
                }
                if (importSettingsXml.MoveToAttribute("RetainObjectIdentity"))
                {
                    importSettings.RetainObjectIdentity = bool.Parse(importSettingsXml.Value);
                }
                if (importSettingsXml.MoveToAttribute("FileCompression"))
                {
                    importSettings.FileCompression = bool.Parse(importSettingsXml.Value);
                }
                if (importSettingsXml.MoveToAttribute("UserInfoUpdate"))
                {
                    importSettings.UserInfoDateTime = (SPImportUserInfoDateTimeOption)Enum.Parse(typeof(SPImportUserInfoDateTimeOption), importSettingsXml.Value);
                }
            }

            importSettingsXml.Close();

            // set other properties which aren't tied to values in XML..
            importSettings.LogFilePath = string.Format("{0}\\{1}.Import.log",
                importSettings.FileLocation, importSettings.BaseFileName);
            
            if (traceSwitchStatic.TraceInfo)
            {
                traceStatic.TraceInfo("CollectImportSettings: Using site URL '{0}'.", importSettings.SiteUrl);
                traceStatic.TraceInfo("CollectImportSettings: Using web URL '{0}'.", importSettings.WebUrl);
                traceStatic.TraceInfo("CollectImportSettings: File location = '{0}'.", importSettings.FileLocation);
                traceStatic.TraceInfo("CollectImportSettings: Base filename = '{0}'.", importSettings.BaseFileName);
                traceStatic.TraceInfo("CollectImportSettings: Update versions = '{0}'.", importSettings.UpdateVersions);
                traceStatic.TraceInfo("CollectImportSettings: Log file path = '{0}'.", importSettings.LogFilePath);
                traceStatic.TraceInfo("CollectImportSettings: Include security = '{0}'.", importSettings.IncludeSecurity);
                traceStatic.TraceInfo("CollectImportSettings: Retain object identity = '{0}'.", importSettings.RetainObjectIdentity);
            }

            if (traceSwitchStatic.TraceVerbose)
            {
                traceStatic.TraceVerbose("CollectImportSettings: Leaving CollectImportSettings().");
            }

            WizardImportSettings wiSettings = new WizardImportSettings(importSettings);
            return wiSettings;
        }

        internal static WizardExportSettings CollectExportSettings(XmlTextReader exportSettingsXml)
        {
            if (traceSwitchStatic.TraceVerbose)
            {
                traceStatic.TraceVerbose("CollectExportSettings: Entered CollectExportSettings().");
            }

            // create SPExportSettings object..
            SPExportSettings exportSettings = new SPExportSettings();
            List<SPObjectData> exportObjectDetails = new List<SPObjectData>();

            if (exportSettingsXml.Name != "ExportSettings")
            {
                exportSettingsXml.Read();
            }

            if (exportSettingsXml.Name == "ExportSettings")
            {
                if (exportSettingsXml.MoveToAttribute("SiteUrl"))
                {
                    exportSettings.SiteUrl = exportSettingsXml.Value;
                }
                if (exportSettingsXml.MoveToAttribute("ExcludeDependencies"))
                {
                    exportSettings.ExcludeDependencies = bool.Parse(exportSettingsXml.Value);
                }
                if (exportSettingsXml.MoveToAttribute("ExportMethod"))
                {
                    exportSettings.ExportMethod =
                        (SPExportMethodType)Enum.Parse(typeof(SPExportMethodType), exportSettingsXml.Value);
                }
                if (exportSettingsXml.MoveToAttribute("IncludeVersions"))
                {
                    exportSettings.IncludeVersions =
                        (SPIncludeVersions)Enum.Parse(typeof(SPIncludeVersions), exportSettingsXml.Value);
                }
                if (exportSettingsXml.MoveToAttribute("IncludeSecurity"))
                {
                    exportSettings.IncludeSecurity =
                        (SPIncludeSecurity)Enum.Parse(typeof(SPIncludeSecurity), exportSettingsXml.Value);
                }
                if (exportSettingsXml.MoveToAttribute("FileCompression"))
                {
                    exportSettings.FileCompression = bool.Parse(exportSettingsXml.Value);
                }
                if (exportSettingsXml.MoveToAttribute("FileLocation"))
                {
                    exportSettings.FileLocation = exportSettingsXml.Value;
                }
                if (exportSettingsXml.MoveToAttribute("BaseFileName"))
                {
                    exportSettings.BaseFileName = exportSettingsXml.Value;
                }
                if (exportSettingsXml.MoveToAttribute("IncludeSecurity"))
                {
                    exportSettings.IncludeSecurity =
                        (SPIncludeSecurity)Enum.Parse(typeof(SPIncludeSecurity), exportSettingsXml.Value);
                }

                exportSettingsXml.MoveToElement();
            }

            while (exportSettingsXml.Read())
            {
                if (exportSettingsXml.Name == "DeploymentObject")
                {
                    SPExportObject exportObject = new SPExportObject();
                    SPObjectData exportObjectData = new SPObjectData();

                    if (exportSettingsXml.MoveToAttribute("Id"))
                    {
                        Guid gID = new Guid(exportSettingsXml.Value);
                        exportObject.Id = gID;
                        exportObjectData.ID = gID;
                    }
                    if (exportSettingsXml.MoveToAttribute("Type"))
                    {
                        SPDeploymentObjectType type = (SPDeploymentObjectType)
                            Enum.Parse(typeof(SPDeploymentObjectType), exportSettingsXml.Value);
                        exportObject.Type = type;
                        exportObjectData.ObjectType = type;
                    }
                    if (exportSettingsXml.MoveToAttribute("ExcludeChildren"))
                    {
                        bool bExcludeChildren = bool.Parse(exportSettingsXml.Value);
                        exportObject.ExcludeChildren = bExcludeChildren;
                        exportObjectData.ExcludeChildren = bExcludeChildren;
                    }
                    if (exportSettingsXml.MoveToAttribute("IncludeDescendants"))
                    {
                        SPIncludeDescendants includeDescs = (SPIncludeDescendants)
                                                            Enum.Parse(typeof(SPIncludeDescendants),
                                                                       exportSettingsXml.Value);
                        exportObject.IncludeDescendants = includeDescs;
                        exportObjectData.IncludeDescendents = includeDescs;
                    }
                    if (exportSettingsXml.MoveToAttribute("Url"))
                    {
                        exportObject.Url = exportSettingsXml.Value;
                        exportObjectData.Url = exportSettingsXml.Value;
                    }
                    if (exportSettingsXml.MoveToAttribute("Title"))
                    {
                        exportObjectData.Title = exportSettingsXml.Value;
                    }

                    exportSettings.ExportObjects.Add(exportObject);
                    exportObjectDetails.Add(exportObjectData);
                }
            }
            
            exportSettingsXml.Close();

            // set other properties which aren't tied to values in XML..
            exportSettings.TestRun = false;
            exportSettings.OverwriteExistingDataFile = true;
            exportSettings.LogFilePath = string.Format("{0}\\{1}.Export.log",
                exportSettings.FileLocation, exportSettings.BaseFileName);
            
            if (traceSwitchStatic.TraceInfo)
            {
                traceStatic.TraceInfo("CollectExportSettings: Using site URL '{0}'.", exportSettings.SiteUrl);
                traceStatic.TraceInfo("CollectExportSettings: Exclude dependencies IDs = '{0}'.", exportSettings.ExcludeDependencies);
                traceStatic.TraceInfo("CollectExportSettings: Export method = '{0}'.", exportSettings.ExportMethod);
                traceStatic.TraceInfo("CollectExportSettings: File location = '{0}'.", exportSettings.FileLocation);
                traceStatic.TraceInfo("CollectExportSettings: Base filename = '{0}'.", exportSettings.BaseFileName);
                traceStatic.TraceInfo("CollectExportSettings: Include versions = '{0}'.", exportSettings.IncludeVersions);
                traceStatic.TraceInfo("CollectExportSettings: Log file path = '{0}'.", exportSettings.LogFilePath);
                traceStatic.TraceInfo("CollectExportSettings: Include security = '{0}'.", exportSettings.IncludeSecurity);
            }

            WizardExportSettings weSettings = new WizardExportSettings(exportSettings, exportObjectDetails);

            if (traceSwitchStatic.TraceVerbose)
            {
                traceStatic.TraceVerbose("CollectExportSettings: Leaving CollectExportSettings().");
            }

            return weSettings;
        }
       
        internal SPObjectData GetSupplementaryData(SPExportObject ExportObject)
        {
            if (traceSwitchStatic.TraceVerbose)
            {
                traceStatic.TraceVerbose("GetSupplementaryData: Entered.");
            }

            SPObjectData foundObject = exportObjectDetails.Find(o => o.ID == ExportObject.Id);
            if (foundObject != null)
            {
                if (traceSwitchStatic.TraceInfo)
                {
                    traceStatic.TraceInfo("GetSupplementaryData: Found object with ID '{0}' in collection.", ExportObject.Id);
                } 
            }
            else
            {
                if (traceSwitchStatic.TraceInfo)
                {
                    traceStatic.TraceInfo("GetSupplementaryData: Did not find object with ID '{0}' in collection, returning null.", ExportObject.Id);
                } 
            }

            if (traceSwitchStatic.TraceVerbose)
            {
                traceStatic.TraceVerbose("GetSupplementaryData: Leaving.");
            }

            return foundObject;
        }

        #endregion

        #region -- Core import/export code --

        public ExportOperationResult RunExport()
        {
            if (traceSwitch.TraceVerbose)
            {
                trace.TraceVerbose("runExportTask: Entered runExportTask().");
            }

            ExportOperationResult exportResult;
            
            // set change token to use if incremental export selected..
            if (ExportSettings.ExportMethod == SPExportMethodType.ExportChanges)
            {
                // first check if we are exporting any site/web objects..
                if (!hasSiteOrWebObject(ExportSettings.ExportObjects))
                {
                    // raise event here if we didn't find a site or web being exported..
                    if (ValidChangeTokenNotFound != null)
                    {
                        ValidChangeTokenNotFound(this, new InvalidChangeTokenEventArgs
                        {
                            EventMessage = WizardDeployment.InvalidObjectsForIncrementalDeploymentMessage
                        });
                    }
                }
                else
                {
                    string lastToken = IncrementalManager.GetLastToken(ExportSettings);
                    if (!string.IsNullOrEmpty(lastToken))
                    {
                        if (traceSwitch.TraceInfo)
                        {
                            trace.TraceInfo(
                                string.Format("runExportTask: Incremental export selected, using change token '{0}'.",
                                              lastToken));
                        }

                        ExportSettings.ExportChangeToken = lastToken;
                    }
                    else
                    {
                        if (traceSwitch.TraceWarning)
                        {
                            trace.TraceWarning(
                                "runExportTask: Attempted to use incremental export selected but no valid change tokens have " +
                                "yet been stored. Defaulting to full export, *current* change token will be stored for future incremental deployments.");
                        }

                        // raise event here if we didn't find a suitable stored change token..
                        if (ValidChangeTokenNotFound != null)
                        {
                            ValidChangeTokenNotFound(this, new InvalidChangeTokenEventArgs
                            {
                                EventMessage = WizardDeployment.ChangeTokenNotFoundMessage
                            });
                        }

                        ExportSettings.ExportMethod = SPExportMethodType.ExportAll;
                    }
                }
            }

            using (SPExport export = new SPExport(ExportSettings))
            {
                export.ProgressUpdated += new EventHandler<SPDeploymentEventArgs>(export_ProgressUpdated);
                export.Completed += new EventHandler<SPDeploymentEventArgs>(export_Completed);

                if (traceSwitch.TraceInfo)
                {
                    trace.TraceInfo("runExportTask: Wired event handlers, about to call Run()..");
                }

                try
                {
                    // initialise to default..
                    exportResult = new ExportOperationResult(ResultType.Failure);

                    export.Run();

                    if (traceSwitch.TraceInfo)
                    {
                        trace.TraceInfo("runExportTask: Export completed successfully.");
                    }

                    // this distinction is to support incremental exports in case the client cares about the token..
                    if (ExportSettings.ExportMethod == SPExportMethodType.ExportAll)
                    {
                        exportResult = new ExportOperationResult(ResultType.Success);
                    }
                    else if (ExportSettings.ExportMethod == SPExportMethodType.ExportChanges)
                    {
                        exportResult = new ExportOperationResult(ResultType.Success, ExportSettings.CurrentChangeToken);
                    }

                    IncrementalManager.SaveToken(ExportSettings.CurrentChangeToken);
                }
                catch (Exception e)
                {
                    if (traceSwitch.TraceError)
                    {
                        trace.TraceError("runExportTask: Exception caught whilst running export: '{0}'.", e);
                    }

                    throw;
                }
            }

            if (traceSwitch.TraceVerbose)
            {
                trace.TraceVerbose("runExportTask: Exiting runExportTask().");
            }

            return exportResult;
        }

        public ImportOperationResult RunImport()
        {
            if (traceSwitch.TraceVerbose)
            {
                trace.TraceVerbose("RunImport: Entered RunImport().");
            }

            ImportOperationResult importResult;
            SPChangeToken startChangeToken = null;
            SPChangeToken endChangeToken = null;
            string destinationRootWebUrl = null;

            if (traceSwitch.TraceInfo)
            {
                trace.TraceInfo("RunImport: Initialising SPSite object with URL '{0}'.",
                    ImportSettings.SiteUrl);
            }

            using (SPSite destinationSite = new SPSite(ImportSettings.SiteUrl))
            {
                // Get the change token from the destination site..
                startChangeToken = destinationSite.CurrentChangeToken;

                if (traceSwitch.TraceInfo)
                {
                    trace.TraceInfo("RunImport: StartChangeToken is '{0}'.",
                        startChangeToken.ToString());
                }

                // Save the root Web URL for future use..
                destinationRootWebUrl = destinationSite.RootWeb.ServerRelativeUrl;

                using (SPImport import = new SPImport(ImportSettings))
                {
                    import.Started += new EventHandler<SPDeploymentEventArgs>(import_Started);
                    import.ObjectImported += new EventHandler<SPObjectImportedEventArgs>(import_ObjectImported);
                    import.ProgressUpdated += new EventHandler<SPDeploymentEventArgs>(import_ProgressUpdated);
                    import.Completed += new EventHandler<SPDeploymentEventArgs>(import_Completed);

                    if (traceSwitch.TraceInfo)
                    {
                        trace.TraceInfo("RunImport: Wired event handlers, about to call Run()..");
                    }

                    // initialise to default..
                    importResult = new ImportOperationResult(ResultType.Failure);

                    try
                    {
                        import.Run();

                        if (traceSwitch.TraceInfo)
                        {
                            trace.TraceInfo("RunImport: Import completed successfully.");
                        }

                        importResult = new ImportOperationResult(ResultType.Success);
                    }
                    catch (Exception e)
                    {
                        if (traceSwitch.TraceError)
                        {
                            trace.TraceError("RunImport: Exception caught whilst running import: '{0}'.", e);
                        }

                        importResult = new ImportOperationResult(ResultType.Failure, e.ToString());
                    }

                    // Get the change token from the destination site AFTER import..
                    endChangeToken = destinationSite.CurrentChangeToken;

                    if (traceSwitch.TraceInfo)
                    {
                        trace.TraceInfo("RunImport: End change token is '{0}', attempting to set publish schedule.", 
                            endChangeToken.ToString());
                    }

                    attemptPublishScheduleSet(startChangeToken, endChangeToken, destinationSite);
                }
            }

            if (traceSwitch.TraceVerbose)
            {
                trace.TraceVerbose("RunImport: Exiting RunImport().");
            }

            return importResult;
        }

        #endregion

        #region -- Events --

        void export_ProgressUpdated(object sender, SPDeploymentEventArgs e)
        {
            if (ProgressUpdated != null)
            {
                ProgressUpdated(this, e);
            }
        }

        void export_Completed(object sender, SPDeploymentEventArgs e)
        {
            if (Completed != null)
            {
                Completed(this, e);
            }
        }

        void import_Started(object sender, SPDeploymentEventArgs e)
        {
            if (Started != null)
            {
                Started(this, e);
            }
        }

        void import_ObjectImported(object sender, SPObjectImportedEventArgs e)
        {
            if (ObjectImported != null)
            {
                ObjectImported(this, e);
            }
        }

        void import_ProgressUpdated(object sender, SPDeploymentEventArgs e)
        {
            if (ProgressUpdated != null)
            {
                ProgressUpdated(this, e);
            }
        }

        void import_Completed(object sender, SPDeploymentEventArgs e)
        {
            if (Completed != null)
            {
                Completed(this, e);
            }
        }
        
        #endregion

        #region -- MOSS-specific publish scheduling code --

        private void attemptPublishScheduleSet(SPChangeToken startChangeToken, SPChangeToken endChangeToken,
            SPSite destinationSite)
        {
            if (traceSwitch.TraceVerbose)
            {
                trace.TraceVerbose("attemptPublishScheduleSet: Entered with site '{0}', start change token '{1}' and " +
                    "end change token '{2}'.", destinationSite.Url, startChangeToken.ToString(), endChangeToken.ToString());
            }

            if (traceSwitch.TraceInfo)
            {
                trace.TraceInfo("attemptPublishScheduleSet: About to reflect 'ScheduledItem' type.");
            }

            Type t = getScheduledItemType();

            if (t != null)
            {
                if (traceSwitch.TraceInfo)
                {
                    trace.TraceInfo("attemptPublishScheduleSet: Successfully reflected 'ScheduledItem' type, about to search " +
                    "'EnableSchedulingOnDeployedItems' method.");
                }

                MethodInfo method = null;
                bool bFoundMethod = false;

                try
                {
                    method = t.GetMethod("EnableSchedulingOnDeployedItems");
                    bFoundMethod = true;

                    if (traceSwitch.TraceInfo)
                    {
                        trace.TraceInfo("attemptPublishScheduleSet: Successfully found 'EnableSchedulingOnDeployedItems' method.");
                    }
                }
                catch (AmbiguousMatchException ame)
                {
                    if (traceSwitch.TraceWarning)
                    {
                        trace.TraceWarning("attemptPublishScheduleSet: Caught exception when reflecting 'EnableSchedulingOnDeployedItems' method. " +
                            "Unable to find method, exception details '{0}'.", ame);
                    }
                }
                catch (ArgumentNullException ane)
                {
                    if (traceSwitch.TraceWarning)
                    {
                        trace.TraceWarning("attemptPublishScheduleSet: Caught exception when reflecting 'EnableSchedulingOnDeployedItems' method. " +
                            "Unable to find method, exception details '{0}'.", ane);
                    }
                }

                if (!bFoundMethod && traceSwitch.TraceWarning)
                {
                    trace.TraceWarning("attemptPublishScheduleSet: Publishing schedule of deployed items will not be honoured!");
                }

                if (traceSwitch.TraceInfo)
                {
                    trace.TraceInfo("attemptPublishScheduleSet: About to call method.");
                }

                try
                {
                    object[] aParams = new object[4] { destinationSite, startChangeToken, endChangeToken, "Succeeded" };

                    method.Invoke(t, aParams);
                }
                catch (Exception e)
                {
                    if (traceSwitch.TraceWarning)
                    {
                        trace.TraceWarning("attemptPublishScheduleSet: Caught exception when running method. Publishing schedule " +
                            "of deployed items will not be honoured! Exception details '{0}.", e);
                    }
                }
            }
            else
            {
                if (traceSwitch.TraceWarning)
                {
                    trace.TraceWarning("attemptPublishScheduleSet: Type was not found, unable to call method. Publishing schedule " +
                        "of deployed items will not be honoured!");
                }
            }

            if (traceSwitch.TraceVerbose)
            {
                trace.TraceVerbose("attemptPublishScheduleSet: Leaving.");
            }
        }

        private Type getScheduledItemType()
        {
            if (traceSwitch.TraceVerbose)
            {
                trace.TraceVerbose("getScheduledItemType: Entered.");
            }

            Assembly publishingAssembly = null;
            Type scheduledItemType = null;

            try
            {
                string sPublishingAssemblyName = getPublishingAssemblyName();

                if (traceSwitch.TraceInfo)
                {
                    trace.TraceInfo("getScheduledItemType: Attempting to load assembly with name '{0}'.",
                        sPublishingAssemblyName);
                }

                publishingAssembly = Assembly.Load(sPublishingAssemblyName);

                if (traceSwitch.TraceInfo)
                {
                    trace.TraceInfo("getScheduledItemType: Successfully loaded assembly - we are in a MOSS environment.",
                        sPublishingAssemblyName);
                }
            }
            catch (Exception asmLoadExc)
            {
                if (traceSwitch.TraceWarning)
                {
                    trace.TraceWarning("getScheduledItemType: Failed to load MOSS assembly - this most likely indicates " +
                        "this is a WSS-only environment. Returning null. Exception details '{0}'.", asmLoadExc);
                }
            }

            if (publishingAssembly != null)
            {
                if (traceSwitch.TraceInfo)
                {
                    trace.TraceInfo("getScheduledItemType: Attempting to reflect 'Microsoft.SharePoint.Publishing.ScheduledItem' type.");
                }

                try
                {
                    scheduledItemType = publishingAssembly.GetType("Microsoft.SharePoint.Publishing.ScheduledItem", true);

                    if (traceSwitch.TraceInfo)
                    {
                        trace.TraceInfo("getScheduledItemType: Successfully got 'Microsoft.SharePoint.Publishing.ScheduledItem' type.");
                    }
                }
                catch (Exception typeLoadExc)
                {
                    if (traceSwitch.TraceWarning)
                    {
                        trace.TraceWarning("getScheduledItemType: Failed to load 'ScheduledItem' type. Returning null. " +
                            "Exception details '{0}'.", typeLoadExc);
                    }
                }
            }

            return scheduledItemType;
        }

        private string getPublishingAssemblyName()
        {
            if (traceSwitch.TraceVerbose)
            {
                trace.TraceVerbose("getPublishingAssemblyName: Entered. About to retrieve appSettings value " +
                    "for key 'SharePointPublishingAssemblyName' (to override default version 12.0.0.0 assembly name).");
            }

            string sAsmName = ConfigurationManager.AppSettings["SharePointPublishingAssemblyName"];

            if (string.IsNullOrEmpty(sAsmName))
            {
                if (traceSwitch.TraceInfo)
                {
                    trace.TraceInfo("getPublishingAssemblyName: No SharePoint publishing assemby override name " +
                        "found in config - using default of '{0}'.", f_csDEFAULT_PUBLISHING_ASSEMBLY_NAME);
                }
                sAsmName = f_csDEFAULT_PUBLISHING_ASSEMBLY_NAME;
            }
            else
            {
                if (traceSwitch.TraceInfo)
                {
                    trace.TraceInfo("getPublishingAssemblyName: Found SharePoint publishing assemby override name " +
                        "in config - using override name of '{0}'.", sAsmName);
                }
            }

            return sAsmName;
        }

        #endregion

        #region -- Helper methods --

        private bool hasSiteOrWebObject(SPExportObjectCollection exportObjects)
        {
            bool bFound = false;
            foreach (SPExportObject exportObject in exportObjects)
            {
                if ((exportObject.Type == SPDeploymentObjectType.Site) || (exportObject.Type == SPDeploymentObjectType.Web))
                {
                    bFound = true;
                    break;
                }
            }

            return bFound;
        }

        #endregion
    }
}