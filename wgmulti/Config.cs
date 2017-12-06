﻿using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Xml.Serialization;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Xml;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace wgmulti
{
  [DataContract(Namespace = "")]
  [XmlRoot(ElementName = "settings")]
  public class Config
  {
    [XmlIgnore]
    public const String configFileName = "WebGrab++.config.xml";

    public const String jsonConfigFileName = "wgmulti.config.json";
    [XmlIgnore]
    public String configFilePath = String.Empty;

    [XmlIgnore]
    public String jsonConfigFilePath = String.Empty;

    [XmlIgnore]
    public String folder = String.Empty;

    [DataMember(Name = "filename", Order = 5), XmlElement("filename")]
    public String outputFilePath = "epg.xml";

    public static String epgFileName = "epg.xml";

    [DataMember(EmitDefaultValue = false, Order = 9), XmlElement]
    public Proxy proxy { get; set; }
    //public List<Credentials> credentials { get; set; }

    [DataMember(Order = 4), XmlElement]
    public String mode = String.Empty;

    [DataMember(EmitDefaultValue = false, Order = 8), XmlElement("user-agent")]
    public String userAgent { get; set; }

    [DataMember(Order = 9), XmlIgnore]
    public bool logging { get; set; }

    [XmlElement("logging")]
    public String Logging
    {
      get { return logging ? "on" : "off"; }
      set { logging = value == "on" || value == "y" || value == "yes" || value == "true"; }
    }

    [DataMember(Order = 7), XmlElement]
    public String skip = "noskip";


    [DataMember(Order = 2, Name = "timespan", IsRequired = true), XmlIgnore]
    public Period period { get; set; }

    [XmlElement("timespan"), IgnoreDataMember]
    public String Period
    {
      get
      {
        var t = period.days.ToString();
        if (!String.IsNullOrEmpty(period.time))
          t += "," + period.time;
        return t;
      }
      set
      {
        if (value.Contains(","))
        {
          var arr = value.Split(',');
          period.days = Convert.ToInt16(arr[0]);
          period.time = arr[1];
        }
        else
          period.days = Convert.ToInt16(value);
      }
    }

    [DataMember(Order = 6), XmlElement]
    public String update = String.Empty;

    [DataMember(Order = 1), XmlElement("retry")]
    public Retry retry { get; set; }

    [DataMember(Order = 8, Name = "postprocess"), XmlElement("postprocess")]
    public PostProcess postProcess { get; set; }

    [DataMember(Order = 10), XmlIgnore]
    public List<Channel> channels
    {
      get;
      set;
    }

    [XmlElement("channel")]
    public List<Channel> Channels
    {
      get {
        //return GetChannels(includeOffset: true, onlyActive: false).ToList();
        return channels;
      }
      set { channels = value; }
    }

    [XmlIgnore]
    public int activeChannels = 0;

    [XmlIgnore]
    public IEnumerable<IGrouping<String, Channel>> grabbers;

    [IgnoreDataMember, XmlIgnore]
    public List<String> DisabledSiteiniNamesList
    {
      get { return disableSiteiniNames ?? new List<String>(); }
      set { DisabledSiteiniNamesList = value; }
    }

    [DataMember(EmitDefaultValue = false, Order = 11, Name = "disabled_siteinis"), XmlIgnore]
    List<String> disableSiteiniNames { get; set; }

    /// <summary>
    /// Returns a list of dates in yyyyMMdd format used when copying EPG
    /// </summary>
    [XmlIgnore, IgnoreDataMember]
    List<String> _dates;

    [XmlIgnore, IgnoreDataMember]
    public List<String> Dates {
      get {
        if (_dates != null && _dates.Count != 0)
          return _dates;
        else
          _dates = new List<String>();

        var today = DateTime.Now;
        for (var i = 0; i < period.days + 1; i++)
          _dates.Add(today.AddDays(i).ToString("yyyyMMdd"));
        return _dates;
      }
      set { }
    }
    

    public Config()
    {
      postProcess = new PostProcess(); //Default postProcess object
      retry = new Retry();
      channels = new List<Channel>();
      period = new Period();
    }
    /// <summary>
    /// Create a config object. If a path to config.xml file is provided, settings will be loaded.
    /// Otherwise a default config file will be created
    /// </summary>
    /// <param name="path">Directory where the configuration exists or will be created</param>
    public Config(String path = null)
    {
      postProcess = new PostProcess(); //init postprocess default values

      if (path == null)
        folder = Arguments.grabingTempFolder;
      else
      {
        folder = path.EndsWith(".xml") ? new FileInfo(path).Directory.FullName : path;
        if (!Path.IsPathRooted(folder))
          throw new ArgumentException("Config folder path must be an absolute path");
      }
      SetAbsPaths(folder);
    }

    /// <summary>
    /// called after cloning a config
    /// </summary>
    /// <param name="configFolder"></param>
    public void SetAbsPaths(String configFolder)
    {
      folder = configFolder; //when called from Clone()
      configFilePath = Path.Combine(folder, configFileName);
      outputFilePath = Path.Combine(folder, epgFileName);
      jsonConfigFilePath = Path.Combine(folder, jsonConfigFileName);
      if (postProcess.run && !Path.IsPathRooted(postProcess.configDir))
        postProcess.configDir = Path.Combine(folder, postProcess.GetFolderName());
    }

    public String Serialize(bool toJson = false)
    {
      if (!toJson)
        return SerializeToXml();
      return SerializeToJson();
    }

    String SerializeToXml()
    {
      try
      {
        // OnSerialize is not supported by XmlSerializer
        channels = GetChannels(includeOffset: true, onlyActive: false).ToList();
        var ser = new XmlSerializer(typeof(Config));
        String buff;
        var settings = new XmlWriterSettings();
        settings.Indent = true;

        using (var ms = new MemoryStream())
        using (var writer = XmlWriter.Create(ms, settings))
        {
          ser.Serialize(writer, this);
          buff = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
        }
        
        return buff
          .Replace(" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"", "")
          .Replace(" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\"", "");
      }
      catch (Exception ex)
      {
        Log.Error(ex.ToString());
        return null;
      }
    }

    String SerializeToJson()
    {
      try
      {
        var ms = new MemoryStream();
        var sr = new DataContractJsonSerializer(typeof(Config));
        var writer = JsonReaderWriterFactory.CreateJsonWriter(ms, Encoding.UTF8, true, true, "  ");
        sr.WriteObject(writer, this);
        writer.Flush();

        return Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
      }
      catch (Exception ex)
      {
        Log.Error(ex.ToString());
        return null;
      }
    }

    public static Config DeserializeFromFile(String configFile)
    {
      if (configFile == null)
        throw new Exception("Specify a config file!");

      Log.Debug("Deserializing config object from file: " + configFile);
      Config conf = configFile.EndsWith(".xml") ? 
        DeserializeXmlFile(configFile) : DeserializeJsonFile(configFile);

      conf.SetAbsPaths(Arguments.configDir);

      if (conf.postProcess.run)
        conf.postProcess.Load();

      Log.Info(String.Format("Config contains {0} channels for grabbing", conf.activeChannels));

      return conf;
    }

    static Config DeserializeXmlFile(String file)
    {
      var ser = new XmlSerializer(typeof(Config));
      var ms = GetMemoryStreamFromFile(file);
      var conf = (Config)ser.Deserialize(ms);

      // XMLSerialization does not support OnDeserialize
      var channels = new List<Channel>();
      var parent_channel = new Channel();

      // We need to create a default SiteIni object for each channel
      foreach (var channel in conf.GetChannels(true))
      {
        // Is this a timeshifted channel, add it to the previous channel 
        if (!String.IsNullOrEmpty(channel.same_as))
        {
          if (parent_channel.timeshifts == null)
            parent_channel.timeshifts = new List<Channel>();
          channel.same_as = parent_channel.xmltv_id;
          parent_channel.timeshifts.Add(channel);
        }
        else
        {
          var siteini = new SiteIni(channel.site, channel.site_id);
          channel.siteinis = new List<SiteIni>();
          channel.siteinis.Add(siteini);
          parent_channel = channel;
          channels.Add(parent_channel);
        }
      }

      conf.channels = channels;

      return conf;
    }

    static MemoryStream GetMemoryStreamFromFile(String file)
    {
      var sb = new StringBuilder();
      using (StreamReader sr = File.OpenText(file))
      {
        String temp;
        while ((temp = sr.ReadLine()) != null)
        {
          temp = Regex.Replace(temp, @"</*channels\s*>", "", RegexOptions.IgnoreCase);
          sb.Append(temp);
        }
        return new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
      }
    }

    static Config DeserializeJsonFile(String file)
    {
      Config conf;
      var txt = File.ReadAllText(file);
      using (var ms = new MemoryStream(Encoding.Unicode.GetBytes(txt)))
      {
        var sr = new DataContractJsonSerializer(typeof(Config));
        conf = (Config) sr.ReadObject(ms);
      }
      return conf;
    }

    /// <summary>
    /// Set the same_as attribute of all timeshifted channels. It should be equal to the parent channel xmltv_id
    /// </summary>
    /// <param name="c"></param>
    [OnDeserialized]
    void OnDeserialized(StreamingContext c)
    {
      foreach (var channel in GetChannels(includeOffset: true))
      {
        // If channel is not timeshifted and has no 'site' attribute, disable it
        if (String.IsNullOrEmpty(channel.same_as) && String.IsNullOrEmpty(channel.site))
        {
          channel.Enabled = false;
        }
      }
    }

    /// <summary>
    /// Saves the config to a file
    /// </summary>
    /// <param name="outputDir">The folder where the config file should be saved</param>
    /// <returns></returns>
    public bool Save(String file = null, bool toJson = false)
    {
      var _filePath = configFilePath;
      if (!String.IsNullOrEmpty(file)) //If we want to overwrite during tests
        _filePath = file;

      try
      {
        if (!Directory.Exists(folder))
          Directory.CreateDirectory(folder);

        File.WriteAllText(_filePath, this.Serialize(toJson));

        // Save postprocess configuration file as well
        if (postProcess.run && !toJson)
          postProcess.Save(folder);

      }
      catch (Exception ex)
      {
        Log.Error(ex.ToString());
        return false;
      }
      return true;
    }

    /// <summary>
    /// Clone a configuration object. 
    /// Don't forget to call Save()
    /// </summary>
    /// <param name="outputFolder">Folder where the XML will be saved</param>
    /// <returns></returns>
    public Config Clone(String outputFolder)
    {
      var newConfig = (Config) this.MemberwiseClone();

      if (this.postProcess.run)
        newConfig.postProcess = postProcess.Clone(outputFolder);

      newConfig.SetAbsPaths(outputFolder);
      return newConfig;
    }

    /// <summary>
    /// Returns enabled channels by various criteria
    /// </summary>
    /// <param name="includeOffset">Include offset channels</param>
    /// <param name="onlyActive">Include only active channels</param>
    /// <param name="withoutPrograms">Include only channels that have no programs</param>
    /// <returns></returns>
    public IEnumerable<Channel> GetChannels(bool includeOffset = false, bool onlyActive = false)
    {
      var parent = new Channel();
      foreach (var channel in channels)
      {
        if (!channel.Enabled || (onlyActive && !channel.active))
          continue;

        parent = channel;
        activeChannels++;
        yield return channel;

        if (!includeOffset || (includeOffset && channel.timeshifts == null))
          continue;

        foreach (var timeshifted in channel.timeshifts)
        {
          if (String.IsNullOrEmpty(timeshifted.same_as))
            timeshifted.same_as = parent.xmltv_id;

          if (timeshifted.Enabled)
          {
            activeChannels++;
            yield return timeshifted;
          }
        }
      }
    }

    public Channel GetChannelByName(String name)
    {
      try
      {
        return GetChannels().First(c => c.name.Equals(name));
      }
      catch (Exception)
      {
      }
      return null;
    }

    public int EmptyChannelsCount()
    {
       return GetChannels().Where(channel => channel.xmltv.programmes.Count == 0).ToList().Count;
    }

    public Xmltv GetChannelGuides()
    {
      Xmltv epg = new Xmltv();
      try
      {
        // Combine all xml guides into a single one
        Log.Info("Merging channel guides");

        foreach (var channel in GetChannels(includeOffset: true))
        {
          if (channel.xmltv.programmes.Count > 0)
          {
            epg.programmes.AddRange(channel.xmltv.programmes);
            epg.postProcessedProgrammes.AddRange(channel.xmltv.postProcessedProgrammes);
            epg.channels.Add(channel.xmltv.channels[0]);
          }
        }
      }
      catch (Exception ex)
      {
        Log.Error("Unable to merge EPGs");
        Log.Error(ex.ToString());
      }
      return epg;
    }

    public override string ToString()
    {
      return "Master channels: " + channels.Count.ToString() + ", Active channels (incl. Offset): " + GetChannels(true).Count();
    }
  }


  public class UpdateType
  {
    public static String None = "";
    public static String Incremental = "i";
    public static String Light = "l";
    public static String Smart = "s";
    public static String Full = "f";
    public static String Index = "index-only";
  }
}
