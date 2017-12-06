﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace wgmulti
{
  public class Report
  {
    public Report(){}

    public List<ChannelInfo> channels = new List<ChannelInfo>();
    public List<String> emptyChannels = new List<String>();
    public int total = 0;
    public int channelsWithoutEpg = 0;
    public int channelsWithEpg = 0;
    public String generationTime = String.Empty;
    public String generatedOn = String.Empty;
    public String fileSize = String.Empty;
    public String md5hash = String.Empty;

    public void Save()
    {
      try
      {
        if (!String.IsNullOrEmpty(Arguments.reportFolder) && !Directory.Exists(Arguments.reportFolder))
          Directory.CreateDirectory(Arguments.reportFolder);

        var serializer = new JavaScriptSerializer();
        var json = serializer.Serialize(this);
        File.WriteAllText(Arguments.reportFilePath, json);
        Console.WriteLine("Report saved to " + Arguments.reportFilePath);
      }
      catch (Exception ex)
      {
        Console.WriteLine(ex.ToString());
      }
    }
  }

  public class ChannelInfo
  {
    public String name = String.Empty;
    public String siteiniName = String.Empty;
    public int siteiniIndex = 0;
    public String firstShowStartsAt = String.Empty;
    public String lastShowStartsAt = String.Empty;
    public int programsCount = 0;

    public ChannelInfo(String name)
    {
      this.name = name;
    }
  }
}
