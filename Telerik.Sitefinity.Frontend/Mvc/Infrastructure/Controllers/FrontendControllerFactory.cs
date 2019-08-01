﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Ninject;
using Telerik.Sitefinity.Frontend.Mvc.Infrastructure.Controllers.Attributes;
using Telerik.Sitefinity.Frontend.Resources;
using Telerik.Sitefinity.Modules.Pages;
using Telerik.Sitefinity.Mvc;
using Telerik.Sitefinity.Pages.Model;

namespace Telerik.Sitefinity.Frontend.Mvc.Infrastructure.Controllers
{
    /// <summary>
    /// This class extends the <see cref="SitefinityControllerFactory"/> by adding additional virtual paths for controller view engines.
    /// </summary>
    public class FrontendControllerFactory : SitefinityControllerFactory
    {
        #region Public members

        /// <summary>
        /// Creates the specified controller by using the specified request context.
        /// </summary>
        /// <returns>The controller.</returns>
        /// <param name="requestContext">
        /// The context of the HTTP request, which includes the HTTP context and route data.
        /// </param>
        /// <param name="controllerName">The name of the controller.</param>
        /// <exception cref="T:System.ArgumentNullException">
        /// The <paramref name="requestContext"/> parameter is null.
        /// </exception>
        /// <exception cref="T:System.ArgumentException">
        /// The <paramref name="controllerName"/> parameter is null or empty.
        /// </exception>
        public override IController CreateController(RequestContext requestContext, string controllerName)
        {
            var baseController = base.CreateController(requestContext, controllerName);
            var controller = baseController as Controller;
            if (controller != null)
            {
                FrontendControllerFactory.EnhanceViewEngines(controller);
            }

            return baseController;
        }

        /// <summary>
        /// Enhances the view engines.
        /// </summary>
        /// <param name="controller">The controller.</param>
        internal static void EnhanceViewEngines(Controller controller)
        {
            var enhanceAttr = FrontendControllerFactory.GetEnhanceAttribute(controller.GetType());
            if (!enhanceAttr.Disabled)
            {
                controller.UpdateViewEnginesCollection(() => FrontendControllerFactory.GetControllerPathTransformations(controller, enhanceAttr.VirtualPath));
            }
        }

        #endregion

        #region Protected members

        /// <inheritdoc />
        protected override IController GetControllerInstance(RequestContext requestContext, Type controllerType)
        {
            object controllerObject = FrontendModule.Current.DependencyResolver.Get(controllerType);
            IController controller = (IController)controllerObject;

            return controller;
        }

        #endregion

        #region Private members

        private static IList<Func<string, string>> GetControllerPathTransformations(Controller controller, string customPath)
        {
            var packagesManager = new PackageManager();
            var currentPackage = packagesManager.GetCurrentPackage();
            var pathTransformations = new List<Func<string, string>>();

            var controllerVp = customPath ?? AppendDefaultPath(FrontendManager.VirtualPathBuilder.GetVirtualPath(controller.GetType().Assembly));
            FrontendControllerFactory.AddDynamicControllerPathTransformations(controller, controllerVp, currentPackage, pathTransformations);

            if (controller.RouteData != null && controller.RouteData.Values.ContainsKey("widgetName"))
            {
                var widgetName = (string)controller.RouteData.Values["widgetName"];
                var controllerType = FrontendManager.ControllerFactory.ResolveControllerType(widgetName);
                var widgetVp = AppendDefaultPath(FrontendManager.VirtualPathBuilder.GetVirtualPath(controllerType));
                pathTransformations.Add(FrontendControllerFactory.GetPathTransformation(widgetVp, currentPackage, widgetName));
            }

            pathTransformations.Add(FrontendControllerFactory.GetPathTransformation(controllerVp, currentPackage));

            var frontendVp = AppendDefaultPath(FrontendManager.VirtualPathBuilder.GetVirtualPath(typeof(FrontendControllerFactory).Assembly));
            if (!string.Equals(controllerVp, frontendVp, StringComparison.OrdinalIgnoreCase))
            {
                pathTransformations.Add(FrontendControllerFactory.GetPathTransformation(frontendVp, currentPackage));
            }

            return pathTransformations;
        }

        private static void AddDynamicControllerPathTransformations(Controller controller, string virtualPath, string currentPackage, List<Func<string, string>> pathTransformations)
        {
            var dynamicControllerWidgetName = controller.ResolveDynamicControllerWidgetName();
            if (string.IsNullOrEmpty(dynamicControllerWidgetName))
                return;

            pathTransformations.Add(FrontendControllerFactory.GetPathTransformation(virtualPath, currentPackage, dynamicControllerWidgetName));
        }

        private static Func<string, string> GetPathTransformation(string controllerVirtualPath, string currentPackage, string widgetName = null)
        {
            return path =>
            {
                var result = path.Replace("~/", "~/" + controllerVirtualPath);

                if (!widgetName.IsNullOrEmpty())
                {
                    // {1} is the ControllerName argument in VirtualPathProviderViewEngines
                    result = result.Replace("{1}", widgetName);
                }

                if (!currentPackage.IsNullOrEmpty())
                    result = result + "#" + currentPackage + Path.GetExtension(path);

                return result;
            };
        }

        private static string AppendDefaultPath(string virtualPath)
        {
            return VirtualPathUtility.AppendTrailingSlash(virtualPath) + "Mvc/";
        }

        private static EnhanceViewEnginesAttribute GetEnhanceAttribute(Type controllerType)
        {
            var enhanceAttr = controllerType.GetCustomAttributes(typeof(EnhanceViewEnginesAttribute), true).FirstOrDefault() as EnhanceViewEnginesAttribute;
            if (enhanceAttr != null)
            {
                return enhanceAttr;
            }

            var key = controllerType.FullName;

            if (!FrontendControllerFactory.EnhanceAttributes.ContainsKey(key))
            {
                lock (FrontendControllerFactory.EnhanceAttributes)
                {
                    if (!FrontendControllerFactory.EnhanceAttributes.ContainsKey(key))
                    {
                        var newEnhanceAttr = new EnhanceViewEnginesAttribute
                        {
                            Disabled = !FrontendControllerFactory.IsInDefaultMvcNamespace(controllerType),
                            VirtualPath = AppendDefaultPath(FrontendManager.VirtualPathBuilder.GetVirtualPath(controllerType.Assembly))
                        };

                        FrontendControllerFactory.EnhanceAttributes.Add(key, newEnhanceAttr);
                    }
                }
            }

            enhanceAttr = FrontendControllerFactory.EnhanceAttributes[key];

            return enhanceAttr;
        }

        private static bool IsInDefaultMvcNamespace(Type controller)
        {
            var expectedTypeName = controller.Assembly.GetName().Name + ".Mvc.Controllers." + controller.Name;
            return string.Equals(expectedTypeName, controller.FullName, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Fields

        private static readonly Dictionary<string, EnhanceViewEnginesAttribute> EnhanceAttributes = new Dictionary<string, EnhanceViewEnginesAttribute>();

        #endregion
    }
}