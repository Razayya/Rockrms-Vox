﻿// <copyright>
// Copyright by BEMA Software Services
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//
using System;

using Rock.Attribute;

namespace com.bemaservices.BemaPipeline.Attribute
{
    /// <summary>
    /// Field Attribute to select 0 or 1 BemaPipeline
    /// </summary>
    [AttributeUsage( AttributeTargets.Class, AllowMultiple = true, Inherited = true )]
    public class BemaPipelineTypeFieldAttribute : FieldAttribute
    {
        private const string INCLUDE_INACTIVE_KEY = "includeInactive";

        /// <summary>
        /// Initializes a new instance of the <see cref="BemaPipelineTypeFieldAttribute" /> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="description">The description.</param>
        /// <param name="required">if set to <c>true</c> [required].</param>
        /// <param name="defaultBemaPipelineTypeId">The default bemaPipeline identifier.</param>
        /// <param name="includeInactive">if set to <c>true</c> [include inactive].</param>
        /// <param name="category">The category.</param>
        /// <param name="order">The order.</param>
        /// <param name="key">The key.</param>
        /// <param name="fieldTypeAssembly">The field type assembly.</param>
        public BemaPipelineTypeFieldAttribute( string name = "Bema Pipeline Type", string description = "", bool required = true, string defaultBemaPipelineTypeId = "", bool includeInactive = false, string category = "", int order = 0, string key = null, string fieldTypeAssembly = "com.bemaservices.RoomManagement" )
            : base( name, description, required, defaultBemaPipelineTypeId, category, order, key, typeof( com.bemaservices.BemaPipeline.Field.Types.BemaPipelineTypeFieldType ).FullName )
        {
            var includeInactiveConfigValue = new Rock.Field.ConfigurationValue( includeInactive.ToString() );
            FieldConfigurationValues.Add( INCLUDE_INACTIVE_KEY, includeInactiveConfigValue );
        }


    }
}