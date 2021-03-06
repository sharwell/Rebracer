﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SLaks.Rebracer.Utilities {
	public static class KnownSettings {
		static readonly HashSet<string> unsafeSubcategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
			// AutoSaveFile allows attackers to make users load all VS settings from a hostile network share
			"Import and Export Settings",
			// ProjectTemplatesLocation allows attackers to host pre-infected project templates from a hostile network share
			"ProjectsAndSolution",
			// HomePage could allow advertising; ViewSourceExternalProgram allows RCE
			"WebBrowser",
			// StartPageRSSUrl could allow advertising; also, it makes no sense for this to be per-solution
			"Startup"
		};

		///<summary>Checks whether a settings category is safe to load from untrusted data.</summary>
		///<returns>False if the category contains properties that could allow an attacker to do harm.</returns>
		public static bool IsAllowed(SettingsSection section) {
			return !unsafeSubcategories.Contains(section.Subcategory);
		}

		static readonly IReadOnlyDictionary<SettingsSection, IReadOnlyCollection<string>> skipProperties = new Dictionary<SettingsSection, IReadOnlyCollection<string>>{
			// Cannot read: Exception occurred. (Exception from HRESULT: 0x80020009 (DISP_E_EXCEPTION))
			{ new SettingsSection("TextEditor", "C/C++ Specific"), new [] { "IntellisenseOptions" } },
			// Cannot write: Member not found. (Exception from HRESULT: 0x80020003 (DISP_E_MEMBERNOTFOUND))
			{ new SettingsSection("TextEditor", "JavaScript Specific"), new [] { "ImplicitReferences" } },

			// Typo'd setting in some VS builds; ignoring to not give warnings for most people.
			{ new SettingsSection("TextEditor", "CSharp-Specific"), new []{ "NewLineQueryExpression_EachClause" } }
		};
		///<summary>Checks whether a specific property should be skipped to to persistence issues.</summary>
		public static bool ShouldSkip(SettingsSection section, string property) {
			IReadOnlyCollection<string> set;
			if (!skipProperties.TryGetValue(section, out set))
				return false;
			return set.Contains(property);
		}

		///<summary>The options categories that should be included by default when creating a new settings file.</summary>
		///<remarks>Existing files will use whatever categories exist in their XML.</remarks>
		[SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "Class is immutable")]
		public static readonly IReadOnlyCollection<SettingsSection> DefaultCategories = new ReadOnlyCollection<SettingsSection>(new[] {
			new SettingsSection("Environment", "TaskList"),
			new SettingsSection("TextEditor", "CSharp-Specific"),
			new SettingsSection("TextEditor", "JavaScript Specific"),
			new SettingsSection("TextEditor", "C/C++ Specific"),
			new SettingsSection("TextEditor", "TypeScript Specific"),
			new SettingsSection("TextEditor", "XAML Specific"),

			//TODO: Which of these categories are used by Venus (<= Dev11) & Libra (>= Dev12)?
			//new SettingsSection("TextEditor", "HTML Specific"),
			// Too slow & unreliable.  https://gist.github.com/SLaks/7999962
			new SettingsSection("TextEditor", "HTMLX Specific"),
		});
	}
	public struct SettingsSection : IEquatable<SettingsSection> {
		public SettingsSection(string category, string subcategory) : this() {
			this.Category = category;
			this.Subcategory = subcategory;
		}

		public static SettingsSection FromXml(XElement subcategoryElement) {
			return new SettingsSection(subcategoryElement.Parent.Attribute("name").Value, subcategoryElement.Attribute("name").Value);
		}
		public static IEnumerable<Tuple<SettingsSection, XElement>> FromXmlSettingsFile(XContainer root) {
			return root.Elements("ToolsOptions")
					   .Elements("ToolsOptionsCategory")
					   .Elements("ToolsOptionsSubCategory")
					   .Select(x => Tuple.Create(FromXml(x), x));
		}

		public string Category { get; private set; }
		public string Subcategory { get; private set; }

		public override bool Equals(object obj) {
			if (!(obj is SettingsSection))
				return false;
			return Equals((SettingsSection)obj);
		}
		public bool Equals(SettingsSection other) {
			return Category == other.Category && Subcategory == other.Subcategory;
		}

		public override int GetHashCode() {
			var hashCode = EqualityComparer<string>.Default.GetHashCode(Category);
			hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Subcategory);
			return hashCode;
		}

		public static bool operator ==(SettingsSection first, SettingsSection second) { return first.Equals(second); }
		public static bool operator !=(SettingsSection first, SettingsSection second) { return !first.Equals(second); }

		public override string ToString() {
			return Category + "/" + Subcategory;
		}
	}
}
