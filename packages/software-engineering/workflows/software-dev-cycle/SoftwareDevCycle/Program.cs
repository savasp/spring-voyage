/*
 * Copyright CVOYA LLC.
 *
 * This source code is proprietary and confidential.
 * Unauthorized copying, modification, distribution, or use of this file,
 * via any medium, is strictly prohibited without the prior written consent of CVOYA LLC.
 */

using Cvoya.Spring.Packages.SoftwareEngineering.Workflows;
using Cvoya.Spring.Packages.SoftwareEngineering.Workflows.Activities;
using Dapr.Workflow;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDaprWorkflow(options =>
{
    options.RegisterWorkflow<SoftwareDevCycleWorkflow>();
    options.RegisterActivity<TriageActivity>();
    options.RegisterActivity<AssignByExpertiseActivity>();
    options.RegisterActivity<CreatePlanActivity>();
    options.RegisterActivity<ImplementActivity>();
    options.RegisterActivity<ReviewActivity>();
    options.RegisterActivity<MergeActivity>();
});

var app = builder.Build();

app.Run();
