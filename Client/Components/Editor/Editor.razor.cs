using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using FileFlows.Shared;
using FileFlows.Shared.Models;
using ffElement = FileFlows.Shared.Models.FlowElement;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Reflection;
using FileFlows.Plugin.Attributes;
using System.Linq;
using System.ComponentModel;
using FileFlows.Client.Components.Inputs;
using FileFlows.Plugin;
using System.Text.Json;
using FileFlows.Client.Components.Dialogs;
using Microsoft.JSInterop;

namespace FileFlows.Client.Components;

public partial class Editor : ComponentBase, IDisposable
{
    [Inject] IJSRuntime jsRuntime { get; set; }

    public bool Visible { get; set; }

    public string Title { get; set; }
    public string HelpUrl { get; set; }
    public string Icon { get; set; }

    private string Uid = Guid.NewGuid().ToString();
    private bool UpdateResizer; // when set to true, the afterrender method will reinitailize the resizer in javascript
    
    protected bool Maximised { get; set; }

    /// <summary>
    /// Get the name of the type this editor is editing
    /// </summary>
    public string TypeName { get; set; }
    protected bool IsSaving { get; set; }

    protected string lblSave, lblSaving, lblCancel, lblClose, lblHelp, lblDownloadButton;

    protected List<ElementField> Fields { get; set; }

    protected Dictionary<string, List<ElementField>> Tabs { get; set; }

    public ExpandoObject Model { get; set; }

    TaskCompletionSource<ExpandoObject> OpenTask;

    public delegate Task<bool> SaveDelegate(ExpandoObject model);
    protected SaveDelegate SaveCallback;

    protected bool ShowDownload { get; set; }
    /// <summary>
    /// Gets if this editor is readonly
    /// </summary>
    public bool ReadOnly { get; private set; }

    /// <summary>
    /// Gets if a confirmation prompt should be shown if there are changes made when the user cancels the editor
    /// </summary>
    public bool PromptUnsavedChanges { get; private set; }
    public bool Large { get; set; }

    public string EditorDescription { get; set; }

    protected readonly List<Inputs.IInput> RegisteredInputs = new List<Inputs.IInput>();

    protected bool FocusFirst = false;
    private bool _needsRendering = false;

    public delegate Task<bool> CancelDeletgate();
    public delegate Task BasicActionDelegate();
    public string DownloadUrl;
    public event CancelDeletgate OnCancel;
    public event BasicActionDelegate OnClosed;

    private string CleanModelJson;


    private RenderFragment _AdditionalFields;
    public RenderFragment AdditionalFields
    {
        get => _AdditionalFields;
        set
        {
            _AdditionalFields = value;
            this.StateHasChanged();
        }
    }

    protected override void OnInitialized()
    {
        lblSave = Translater.Instant("Labels.Save");
        lblSaving = Translater.Instant("Labels.Saving");
        lblCancel = Translater.Instant("Labels.Cancel");
        lblClose = Translater.Instant("Labels.Close");
        lblHelp = Translater.Instant("Labels.Help");
        this.Maximised = false;
        App.Instance.OnEscapePushed += InstanceOnOnEscapePushed;
    }

    private void InstanceOnOnEscapePushed(OnEscapeArgs args)
    {
        if (args.HasModal || this.Visible == false || args.HasLogPartialViewer)
            return;
        Cancel();
    }

    protected override void OnAfterRender(bool firstRender)
    {
        if(UpdateResizer)
            jsRuntime.InvokeVoidAsync("ff.resizableEditor", this.Uid);
        if (FocusFirst)
        {
            foreach (var input in RegisteredInputs)
            {
                if (input.Focus())
                    break;
            }
            FocusFirst = false;
        }
    }

    private ExpandoObject ConvertToExando(object model)
    {
        if (model == null)
            return new ExpandoObject();
        if (model is ExpandoObject eo)
            return eo;

        var expando = new ExpandoObject();
        var dictionary = (IDictionary<string, object>)expando;

        foreach (var property in model.GetType().GetProperties())
            dictionary.Add(property.Name, property.GetValue(model));
        return expando;
    }


    internal void RegisterInput<T>(Input<T> input)
    {
        if (this.RegisteredInputs.Contains(input) == false)
            this.RegisteredInputs.Add(input);
    }

    internal void RemoveRegisteredInputs(params string[] except)
    {
        var listExcept = except?.ToList() ?? new();
        this.RegisteredInputs.RemoveAll(x => listExcept.Contains(x.Field?.Name ?? string.Empty) == false);
    }

    internal Inputs.IInput GetRegisteredInput(string name)
    {
        return this.RegisteredInputs.Where(x => x.Field.Name == name).FirstOrDefault();
    }

    /// <summary>
    /// Opens an editor
    /// </summary>
    /// <param name="typeName">the type name of the bound model, used for translations</param>
    /// <param name="title">the title of the editor</param>
    /// <param name="fields">the main fields to show in the editor</param>
    /// <param name="model">the model to bind to the editor</param>
    /// <param name="saveCallback">a callback that is called when the editor is saved</param>
    /// <param name="readOnly">if the editor is readonly</param>
    /// <param name="large">if the editor is a large editor and takes up more width</param>
    /// <param name="lblSave">the label to show on the save button</param>
    /// <param name="lblCancel">the label to show on the cancel button</param>
    /// <param name="additionalFields">any additional fields ot show</param>
    /// <param name="tabs">the tabs for the editor</param>
    /// <param name="helpUrl">the URL for the help button</param>
    /// <param name="noTranslateTitle">it the title should not be translated</param>
    /// <param name="lblDownloadButton">the label to shown on the download button</param>
    /// <param name="downloadUrl">the URL for the download button</param>
    /// <param name="promptUnsavedChanges">if a prompt should be shown the the user if they try to close the editor with changes</param>
    /// <returns>the updated model from the edit</returns>
    internal Task<ExpandoObject> Open(string typeName, string title, List<ElementField> fields, object model, SaveDelegate saveCallback = null, bool readOnly = false, bool large = false, string lblSave = null, string lblCancel = null, RenderFragment additionalFields = null, Dictionary<string, List<ElementField>> tabs = null, string helpUrl = null, bool noTranslateTitle = false, string lblDownloadButton = "Labels.Download", string downloadUrl = null, bool promptUnsavedChanges = true)
    {
        this.RegisteredInputs.Clear();
        var expandoModel = ConvertToExando(model);
        this.Model = expandoModel;
        this.SaveCallback = saveCallback;
        this.PromptUnsavedChanges = promptUnsavedChanges;
        if (promptUnsavedChanges && readOnly == false) 
            this.CleanModelJson = ModelToJsonForCompare(expandoModel);
        this.TypeName = typeName;
        this.Maximised = false;
        this.Uid = Guid.NewGuid().ToString();
        this.UpdateResizer = true;
        if (noTranslateTitle)
            this.Title = title;
        else
            this.Title = Translater.TranslateIfNeeded(title);
        this.Fields = fields;
        this.Tabs = tabs;
        this.ReadOnly = readOnly;
        this.Large = large;
        this.ShowDownload = string.IsNullOrWhiteSpace(downloadUrl) == false;
        this.lblDownloadButton = Translater.TranslateIfNeeded(lblDownloadButton);
        this.DownloadUrl = downloadUrl;
        this.Visible = true;
        this.HelpUrl = helpUrl ?? string.Empty;
        this.AdditionalFields = additionalFields;


        lblSave = lblSave.EmptyAsNull() ?? "Labels.Save";
        this.lblCancel = Translater.TranslateIfNeeded(lblCancel.EmptyAsNull() ?? "Labels.Cancel");

        if (lblSave == "Labels.Save") {
            this.lblSaving = Translater.Instant("Labels.Saving");
            this.lblSave = Translater.Instant(lblSave);
        }
        else
        {
            this.lblSave = Translater.Instant(lblSave);
            this.lblSaving = lblSave;
        }

        this.EditorDescription = Translater.Instant(typeName + ".Description");
        OpenTask = new TaskCompletionSource<ExpandoObject>();
        this.FocusFirst = true;
        this.StateHasChanged();
        return OpenTask.Task;
    }

    protected async Task WaitForRender()
    {
        _needsRendering = true;
        StateHasChanged();
        while (_needsRendering)
        {
            await Task.Delay(50);
        }
    }

    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        _needsRendering = false;
        return base.OnAfterRenderAsync(firstRender);
    }


    protected async Task OnSubmit()
    {
        await this.Save();
    }

    protected async Task OnClose()
    {
        this.Cancel();
    }

    protected async Task Save()
    {
        if (ReadOnly)
        {
            Logger.Instance.ILog("Cannot save, readonly");
            return;
        }

        bool valid = true;
        foreach (var input in RegisteredInputs)
        {
            bool iValid = await input.Validate();
            if (iValid == false)
            {
                Logger.Instance.DLog("Invalid input:" + input.Label);
                valid = false;
            }
        }
        if (valid == false)
            return;

        if (SaveCallback != null)
        {
            bool saved = await SaveCallback(this.Model);
            if (saved == false)
                return;
        }
        OpenTask?.TrySetResult(this.Model);

        this.Visible = false;
        this.Fields?.Clear();
        this.Tabs?.Clear();
        this.OnClosed?.Invoke();
    }

    protected async void Cancel()
    {
        if(OnCancel != null)
        {
            bool result = await OnCancel.Invoke();
            if (result == false)
                return;
        }

        if (PromptUnsavedChanges && ReadOnly == false)
        {
            string currentModelJson = ModelToJsonForCompare(Model);
            if(currentModelJson != CleanModelJson)
            {
                Logger.Instance.ILog("CleanModelJson");
                Logger.Instance.ILog(CleanModelJson);
                Logger.Instance.ILog("currentModelJson");
                Logger.Instance.ILog(currentModelJson);
                bool confirmResult = await Confirm.Show("Labels.Confirm", "Labels.CancelMessage");
                if(confirmResult == false)
                    return;
            }
        }

        OpenTask?.TrySetCanceled();
        this.Visible = false;
        if(this.Fields != null)
            this.Fields.Clear();
        if(this.Tabs != null)
            this.Tabs.Clear();

        await this.WaitForRender();
        this.OnClosed?.Invoke();
    }

    private string ModelToJsonForCompare(ExpandoObject model)
    {
        string json = model == null ? string.Empty : JsonSerializer.Serialize(Model);
        json = json.Replace("[]", "null");
        return json;
    }

    /// <summary>
    /// Finds a field by its name
    /// </summary>
    /// <param name="name">the name of the field</param>
    /// <returns>the field if found, otherwise null</returns>
    internal ElementField? FindField(string name)
    {
        var field = this.Fields?.Where(x => x.Name == name)?.FirstOrDefault();
        return field;
    }
    
    /// <summary>
    /// Updates a value
    /// </summary>
    /// <param name="field">the field whose value is being updated</param>
    /// <param name="value">the value of the field</param>
    internal void UpdateValue(ElementField field, object value)
    {
        if (field.UiOnly)
            return;
        if (Model == null)
            return;
        var dict = (IDictionary<string, object>)Model;
        if (dict.ContainsKey(field.Name))
            dict[field.Name] = value;
        else
            dict.Add(field.Name, value);
    }

    /// <summary>
    /// Gets a parameter value for a field
    /// </summary>
    /// <param name="field">the field to get the value for</param>
    /// <param name="parameter">the name of the parameter</param>
    /// <param name="default">the default value if not found</param>
    /// <typeparam name="T">the type of parameter</typeparam>
    /// <returns>the parameter value</returns>
    internal T GetParameter<T>(ElementField field, string parameter, T @default = default(T))
    {
        var dict = field?.Parameters as IDictionary<string, object>;
        if (dict?.ContainsKey(parameter) != true)
            return @default;
        var val = dict[parameter];
        if (val == null)
            return @default;
        try
        {
            var converted = Converter.ConvertObject(typeof(T), val);
            T result = (T)converted;
            if(result is List<ListOption> options)
            {
                foreach(var option in options)
                {
                    if(option.Value is JsonElement je)
                    {
                        if (je.ValueKind == JsonValueKind.String)
                            option.Value = je.GetString();
                        else if (je.ValueKind == JsonValueKind.Number)
                            option.Value = je.GetInt32();
                    }
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            Logger.Instance.ELog("Failed converted: " + parameter, val);
            return @default;
        }
    }
    
    /// <summary>
    /// Gets the minimum and maximum from a range validator (if exists)
    /// </summary>
    /// <param name="field">The field to get the range for</param>
    /// <returns>the range</returns>
    internal (int min, int max) GetRange(ElementField field)
    {
        var range = field?.Validators?.Where(x => x is FileFlows.Shared.Validators.Range)?.FirstOrDefault() as FileFlows.Shared.Validators.Range;
        return range == null ? (0, 0) : (range.Minimum, range.Maximum);
    }

    /// <summary>
    /// Gets the default of a specific type
    /// </summary>
    /// <param name="type">the type</param>
    /// <returns>the default value</returns>
    private object GetDefault(Type type)
    {
        if(type?.IsValueType == true)
        {
            return Activator.CreateInstance(type);
        }
        return null;
    }

    /// <summary>
    /// Gets a value for a field
    /// </summary>
    /// <param name="field">the field whose value to get</param>
    /// <param name="type">the type of value to get</param>
    /// <returns>the value</returns>
    internal object GetValue(string field, Type type)
    {
        if (Model == null)
            return GetDefault(type);
        
        var dict = (IDictionary<string, object>)Model;
        if (dict.ContainsKey(field) == false)
            return GetDefault(type);
        object value = dict[field];
        if (value == null)
            return GetDefault(type);

        if (value is JsonElement je)
        {
            if (type == typeof(string))
                return je.GetString();
            if (type== typeof(int))
                return je.GetInt32();
            if (type == typeof(bool))
                return je.GetBoolean();
            if (type == typeof(float))
                return (float)je.GetInt64();
        }

        if (value.GetType().IsAssignableTo(type))
        {
            return value;
        }

        try
        {
            return Converter.ConvertObject(type, value);
        }
        catch(Exception)
        {
            return GetDefault(type);
        }
    }
    
    /// <summary>
    /// Gets a value for a field
    /// </summary>
    /// <param name="field">the field whose value to get</param>
    /// <param name="default">the default value if none is found</param>
    /// <typeparam name="T">the type of value to get</typeparam>
    /// <returns>the value</returns>
    internal T GetValue<T>(string field, T @default = default(T))
    {
        if (Model == null)
            return @default;
        var dict = (IDictionary<string, object>)Model;
        if (dict.ContainsKey(field) == false)
        {
            return @default;
        }
        object value = dict[field];
        if (value == null)
        {
            return @default;
        }

        if (value is JsonElement je)
        {
            if (typeof(T) == typeof(string))
                return (T)(object)je.GetString();
            if (typeof(T) == typeof(int))
                return (T)(object)je.GetInt32();
            if (typeof(T) == typeof(bool))
                return (T)(object)je.GetBoolean();
            if (typeof(T) == typeof(float))
            {
                try
                {
                    return (T)(object)(float)je.GetInt64();
                }
                catch (Exception)
                {
                    return (T)(object)(float.Parse(je.ToString()));
                }
            }
        }

        if (value is T)
        {
            return (T)value;
        }

        try
        {
            return (T)Converter.ConvertObject(typeof(T), value);
        }
        catch(Exception)
        {
            return default;
        }
    }

    protected void OpenHelp()
    {
        if (string.IsNullOrWhiteSpace(HelpUrl))
            return;
        _ = jsRuntime.InvokeVoidAsync("open", HelpUrl.ToLower(), "_blank");
    }

    protected void OnMaximised(bool maximised)
    {
        this.Maximised = maximised;
    }

    public void Dispose()
    {
        App.Instance.OnEscapePushed -= InstanceOnOnEscapePushed;
    }
}