﻿// <copyright file="WorkItemsPage.cs" company="Microsoft Corporation">Copyright Microsoft Corporation. All Rights Reserved. This code released under the terms of the Microsoft Public License (MS-PL, http://opensource.org/licenses/ms-pl.html.) This is sample code only, do not use in production environments.</copyright>
namespace Microsoft.ALMRangers.Samples.MyHistory
{
    using System;
    using System.Collections.ObjectModel;
    using System.Threading.Tasks;
    using EnvDTE;
    using Microsoft.TeamFoundation.Client;
    using Microsoft.TeamFoundation.Controls;
    using Microsoft.TeamFoundation.WorkItemTracking.Client;
    using Microsoft.VisualStudio.TeamFoundation.WorkItemTracking;

    /// <summary>
    /// We are extending Team Explorer by adding a new page and therefore use the TeamExplorerPage attribute and pass in our unique ID
    /// </summary>
    [TeamExplorerPage(WorkItemsPage.PageId)]
    public class WorkItemsPage : TeamExplorerBasePage
    {
        // All Pages must have a unique ID. Use the Tools - Create GUID menu in Visual Studio to create your own GUID
        public const string PageId = "E47AACBD-4AC1-4A9B-9F96-9D2256D5B1E4";
        private ObservableCollection<WorkItem> workItems = new ObservableCollection<WorkItem>();

        public WorkItemsPage()
        {
            // Set the page title
            this.Title = "My History - WorkItems";
            this.PageContent = new WorkItemsPageView();
            this.View.ParentSection = this;
        }

        public ObservableCollection<WorkItem> WorkItems
        {
            get
            {
                return this.workItems;
            }

            protected set
            {
                this.workItems = value;
                this.RaisePropertyChanged("WorkItems");
            }
        }

        protected WorkItemsPageView View
        {
            get { return this.PageContent as WorkItemsPageView; }
        }

        public void ViewWorkItemDetails(int workItemId)
        {
            try
            {
                ITeamFoundationContext context = this.CurrentContext;
                EnvDTE80.DTE2 dte2 = Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(DTE)) as EnvDTE80.DTE2;
                if (dte2 != null)
                {
                    DocumentService witDocumentService = (DocumentService)dte2.DTE.GetObject("Microsoft.VisualStudio.TeamFoundation.WorkItemTracking.DocumentService");
                    var widoc = witDocumentService.GetWorkItem(context.TeamProjectCollection, workItemId, this);
                    witDocumentService.ShowWorkItem(widoc);
                }
            }
            catch (Exception ex)
            {
                this.ShowNotification(ex.Message, NotificationType.Error);
            }
        }

        public async override void Initialize(object sender, PageInitializeEventArgs e)
        {
            base.Initialize(sender, e);

            // If the user navigated back to this page, there could be saved context information that is passed in
            var sectionContext = e.Context as ChangesSectionContext;
            if (sectionContext != null)
            {
                // Restore the context instead of refreshing
                ChangesSectionContext context = sectionContext;
                this.WorkItems = context.WorkItems;
            }
            else
            {
                // Kick off the refresh
                await this.RefreshAsync();
            }
        }

        /// <summary>
        /// Refresh override.
        /// </summary>
        public async override void Refresh()
        {
            base.Refresh();
            await this.RefreshAsync();
        }

        /// <summary>
        /// Save contextual information about the current section state.
        /// </summary>
        public override void SaveContext(object sender, PageSaveContextEventArgs e)
        {
            base.SaveContext(sender, e);

            // Save our current so when the user navigates back to the page the content is restored rather than requeried
            ChangesSectionContext context = new ChangesSectionContext { WorkItems = this.WorkItems };
            e.Context = context;
        }

        /// <summary>
        /// ContextChanged override.
        /// </summary>
        protected override async void ContextChanged(object sender, TeamFoundation.Client.ContextChangedEventArgs e)
        {
            base.ContextChanged(sender, e);

            // If the team project collection or team project changed, refresh the data for this section
            if (e.TeamProjectCollectionChanged || e.TeamProjectChanged)
            {
                await this.RefreshAsync();
            }
        }

        private async Task RefreshAsync()
        {
            try
            {
                // Set our busy flag and clear the previous data
                this.IsBusy = true;
                this.WorkItems.Clear();

                ObservableCollection<WorkItem> lworkItems = new ObservableCollection<WorkItem>();

                // Make the server call asynchronously to avoid blocking the UI
                await Task.Run(() =>
                {
                    ITeamFoundationContext context = this.CurrentContext;
                    if (context != null && context.HasCollection && context.HasTeamProject)
                    {
                        WorkItemStore wis = context.TeamProjectCollection.GetService<WorkItemStore>();
                        if (wis != null)
                        {
                            WorkItemCollection wic = wis.Query("SELECT [System.Id], [System.Title], [System.State] FROM WorkItems WHERE [System.WorkItemType] <> ''  AND  [System.State] <> ''  AND  [System.AssignedTo] EVER @Me ORDER BY [System.ChangedDate] desc");
                            int i = 0;
                            foreach (WorkItem wi in wic)
                            {
                                lworkItems.Add(wi);
                                i++;
                                if (i >= 250)
                                {
                                    break;
                                }
                            }
                        }
                    }
                });

                // Now back on the UI thread, update the bound collection and section title
                this.WorkItems = lworkItems;
            }
            catch (Exception ex)
            {
                this.ShowNotification(ex.Message, NotificationType.Error);
            }
            finally
            {
                // Always clear our busy flag when done
                this.IsBusy = false;
            }
        }
    }
}