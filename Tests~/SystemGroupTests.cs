using EOS.Attributes;
using EOS.Core;
using EOS.Entities;
using EOS.Extensions;
using EOS.Objects;
using EOS.Systems;
using EOS.Systems.Groups;
using Xunit;

namespace EOS.Tests
{
    public sealed class GroupTestGroup : SystemGroup { }
    public class ParentTestGroup : SystemGroup { }
    public sealed class ChildTestGroup : ParentTestGroup { }

    public sealed class GroupProbe : EosObject { public int Count; }
    public sealed class HierProbe : EosObject { public int Count; }

    [Group(typeof(GroupTestGroup))]
    public sealed class GroupedSystem : EosSystem
    {
        void Execute(GroupProbe p) => p.Count++;
    }

    [Group(typeof(ChildTestGroup))]
    public sealed class ChildGroupedSystem : EosSystem
    {
        void Execute(HierProbe p) => p.Count++;
    }

    public sealed class SystemGroupTests
    {
        static World NewWorld()
        {
            var world = new World();
            world.Init();
            return world;
        }

        [Fact]
        public void Group_EnabledByDefault_RunsSystem()
        {
            var world = NewWorld();
            var e = new EosEntity(world, "e", true);
            e.Add<GroupProbe>();

            world.Update(0f);

            Assert.Equal(1, e.Get<GroupProbe>().Count);
        }

        [Fact]
        public void DisablingGroup_StopsSystem_ReenablingResumes()
        {
            var world = NewWorld();
            var e = new EosEntity(world, "e", true);
            e.Add<GroupProbe>();

            world.Update(0f);
            Assert.Equal(1, e.Get<GroupProbe>().Count);

            world.SystemGroups.Disable<GroupTestGroup>();
            world.Update(0f);
            Assert.Equal(1, e.Get<GroupProbe>().Count);

            world.SystemGroups.Enable<GroupTestGroup>();
            world.Update(0f);
            Assert.Equal(2, e.Get<GroupProbe>().Count);
        }

        [Fact]
        public void DisablingParentGroup_DisablesChildSystem()
        {
            var world = NewWorld();
            var e = new EosEntity(world, "e", true);
            e.Add<HierProbe>();

            world.SystemGroups.Disable<ParentTestGroup>();
            world.Update(0f);

            Assert.Equal(0, e.Get<HierProbe>().Count);
        }
    }
}
