using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Data.Entity.ModelConfiguration;
using System.Linq;
using System.Runtime.Serialization;
using com.bemaservices.BemaPipeline.BemaPipelineActionTypes;
using com.bemaservices.BemaPipeline.Web.Cache;
using Rock;
using Rock.Data;
using Rock.Model;
namespace com.bemaservices.BemaPipeline.Rest.API
{
    public class ActionLink : DotLiquid.Drop
    {
        /// <summary>
        /// Action Id, which the client will use to call api endpoint
        /// </summary>
        public int ActionId { get; set; }

        /// <summary>
        /// Url to redirect user to. Intended
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Text label for button, like 'Start Workflow'
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Icon Css Class, like 'fa fa-list'
        /// </summary>
        public string IconCssClass { get; set; }

        /// <summary>
        /// Class used to describe bootstrap button styling, like btn-primary.
        /// </summary>
        public string ButtonUtilityClass { get; set; }

    }

}