using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MakingSense.AspNetCore.HypermediaApi.Linking;
using MakingSense.AspNetCore.HypermediaApi.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Internal;

namespace MakingSense.AspNetCore.HypermediaApi.ExceptionHandling
{
	public class ProblemResult : IActionResult
	{
		// TODO: Turn this class into a POCO and delegate rendering to a middleware

		const string PROBLEM_MEDIATYPE = "application/problem+json";

		private readonly Problem _problem;

		public ProblemResult(Problem problem)
		{
			_problem = problem;
		}

		public async Task ExecuteResultAsync(ActionContext context)
		{
			ObjectResult objectResult = ExecuteFirstStep(context);
			await objectResult.ExecuteResultAsync(context);
		}

		public async Task ExecuteResultAsync(ObjectResultExecutor executor, ActionContext context)
		{
			ObjectResult objectResult = ExecuteFirstStep(context);
			await executor.ExecuteAsync(context, objectResult);
		}

		private ObjectResult ExecuteFirstStep(ActionContext context)
		{
			_problem.InjectContext(context.HttpContext);

			//TODO: If request ACCEPT header accepts HTML and does not accept JSON and
			//path begins with `docs/`, render error as HTML
			//See DocumentationMiddleware TODO note

			// Only set the right content-type (application/problem+json) if it is accepted
			// otherwise return application/json + schema
			var acceptsProblemType = context.HttpContext.Request.Headers[HeaderNames.Accept].Contains(PROBLEM_MEDIATYPE);

			context.HttpContext.Response.OnStarting((o) =>
			{
				context.HttpContext.Response.ContentType = acceptsProblemType ? PROBLEM_MEDIATYPE
					: context.HttpContext.Response.ContentType + $"; profile={SchemaAttribute.Path}problem.json";
				return Task.FromResult(0);
			}, null);

			foreach (var pair in _problem.GetCustomHeaders())
			{
				context.HttpContext.Response.Headers[pair.Key] = pair.Value;
			}

			// Set problem status code
			context.HttpContext.Response.StatusCode = _problem.status;

			// Add a link to home page
			var linkHelper = context.HttpContext.RequestServices.GetService<ILinkHelper>();
			if (linkHelper != null)
			{
				// Only add self link in GET errors
				if ("GET".Equals(context.HttpContext.Request.Method, StringComparison.OrdinalIgnoreCase))
				{
					_problem._links.Add(linkHelper.ToSelf());
				}

				// TODO: Add better links for errors, and allow to customize them
				// For example, in non-GET errors add a self link to the related resource (GET relation)

				_problem._links.Add(linkHelper.ToHomeAccount());
			}

			return new ObjectResult(_problem);
		}
	}
}