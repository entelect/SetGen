using System;
using System.Collections;

namespace SetGen
{
    public class Project : IComparable, IComparable<Project>, IEqualityComparer
    {
        public string ProjectName { get; set; }
        public string ProjectDirectory { get; set; }
        public string AzureProjectName { get; set; }
        public string AzureProjectDirectory { get; set; }

        public override bool Equals(object obj)
        {
            Project other = obj as Project;
            if (other != null)
                return ProjectName.Equals(other.ProjectName, StringComparison.OrdinalIgnoreCase);
            return false;
        }

        public override int GetHashCode()
        {
            return ProjectName.GetHashCode();
        }

        public int CompareTo(object obj)
        {
            Project other = obj as Project;
            if (other != null)
                return other.ProjectName.CompareTo(ProjectName);
            return -1;
        }

        public int CompareTo(Project other)
        {
            return other.ProjectName.CompareTo(ProjectName);
        }

        public bool Equals(object x, object y)
        {
            Project a = x as Project;
            Project b = y as Project;
            if (a == null || b == null)
                return false;
            return a.Equals(b);
        }

        public int GetHashCode(object obj)
        {
            Project project = obj as Project;
            if (project != null)
                return project.GetHashCode();
            return obj.GetHashCode();
        }
    }
}
