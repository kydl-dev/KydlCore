using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;

namespace KydlCore.Tests.Unit;

/// <summary>T-098: the data side of the page rows (toggle, slider, text box, list): what a mod hands over arrives as given, and wrong input is made harmless. The drawing is game code.</summary>
[TestFixture]
public class PageRowsTests
{
    static readonly Type PageT = ModAssembly.Type("KydlCore.Features.Hub.Contracts.KyPage");

    static object Section(out object page)
    {
        page = Activator.CreateInstance(PageT);
        return PageT.GetMethod("Section").Invoke(page, new object[] { "" });
    }

    static IList Rows(object section) { return (IList)section.GetType().GetProperty("Rows").GetValue(section, null); }
    static object Prop(object o, string name) { return o.GetType().GetProperty(name).GetValue(o, null); }

    [Test]
    public void Slider_MaxBelowMin_IsRaisedToMin_AndEmptyFormatGetsDefault()
    {
        object page; var s = Section(out page);
        s.GetType().GetMethod("Slider").Invoke(s, new object[] { "L", 5f, 1f, 3f, "", null });
        var row = Rows(s)[0];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Prop(row, "Max"), Is.EqualTo(5f));
            Assert.That(Prop(row, "Format"), Is.EqualTo("0.##"));
            Assert.That(Prop(row, "Value"), Is.EqualTo(3f));
        }
    }

    [Test]
    public void Toggle_KeepsStateAndCallback()
    {
        object page; var s = Section(out page);
        bool got = false;
        Action<bool> cb = v => got = v;
        s.GetType().GetMethod("Toggle").Invoke(s, new object[] { "On?", true, cb });
        var row = Rows(s)[0];
        Assert.That(Prop(row, "On"), Is.EqualTo(true));
        ((Action<bool>)Prop(row, "Changed"))(true);
        Assert.That(got, Is.True);
    }

    [Test]
    public void TextBox_NullValueAndZeroLength_GetSafeDefaults()
    {
        object page; var s = Section(out page);
        s.GetType().GetMethod("TextBox").Invoke(s, new object[] { "Name", null, 0, null });
        var row = Rows(s)[0];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Prop(row, "Value"), Is.EqualTo(""));
            Assert.That(Prop(row, "MaxLength"), Is.EqualTo(64));
        }
    }

    [Test]
    public void List_NullItems_IsEmpty_AndItemsArrive()
    {
        object page; var s = Section(out page);
        s.GetType().GetMethod("List").Invoke(s, new object[] { "Title", null });
        s.GetType().GetMethod("List").Invoke(s, new object[] { "", new List<string> { "a", "b" } });
        using (Assert.EnterMultipleScope())
        {
            Assert.That(((ICollection)Prop(Rows(s)[0], "Items")), Is.Empty);
            Assert.That(((ICollection)Prop(Rows(s)[1], "Items")).Count, Is.EqualTo(2));
        }
    }
}
