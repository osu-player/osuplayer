﻿using System;
using System.Globalization;
using Material.Icons;
using NUnit.Framework;
using OsuPlayer.Data.OsuPlayer.Enums;
using OsuPlayer.Extensions.ValueConverters;

namespace OsuPlayer.Tests.ValueConverterTests;

public class RepeatConverterTests
{
    private readonly Type _expectedInput = typeof(RepeatMode);
    private readonly Type _expectedOutput = typeof(MaterialIconKind);
    private RepeatConverter _repeatConverter;

    [SetUp]
    public void Setup()
    {
        _repeatConverter = new RepeatConverter();
    }

    [TestCase(10)]
    [TestCase("test")]
    public void TestWrongInputHandled(object input)
    {
        Assert.IsNotInstanceOf(_expectedInput, input.GetType());
        Assert.DoesNotThrow(() => _repeatConverter.Convert(input, _expectedOutput, null, CultureInfo.InvariantCulture));
    }

    [Test]
    public void TestNullInputHandled()
    {
        Assert.DoesNotThrow(() => _repeatConverter.Convert(null, _expectedOutput, null, CultureInfo.InvariantCulture));
    }

    [TestCase(RepeatMode.NoRepeat)]
    [TestCase(RepeatMode.Playlist)]
    public void TestCorrectUsage(RepeatMode test)
    {
        var output = _repeatConverter.Convert(test, _expectedOutput, null, CultureInfo.InvariantCulture);
        Assert.IsInstanceOf(_expectedOutput, output);
    }

    [TestCase(new object[] {RepeatMode.NoRepeat, false})]
    [TestCase(new object[] {RepeatMode.Playlist, true})]
    public void TestCorrectBoolUsage(object[] input)
    {
        var type = typeof(bool);
        
        var output = _repeatConverter.Convert(input[0], type, null, CultureInfo.InvariantCulture);
        Assert.IsInstanceOf(type, output);
        Assert.AreEqual(output, input[1]);
    }

    [Test]
    public void TestOutputOnIncorrectInput()
    {
        var output = _repeatConverter.Convert(10, _expectedOutput, null, CultureInfo.InvariantCulture);
        Assert.IsInstanceOf(_expectedOutput, output);
        Assert.AreEqual(output, MaterialIconKind.QuestionMark);
    }
}