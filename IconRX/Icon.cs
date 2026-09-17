using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;

namespace IconRX;

public class Icon : Control
{
    static Icon()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(Icon),
            new FrameworkPropertyMetadata(typeof(Icon)));
    }

    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(
            nameof(Source), typeof(string), typeof(Icon),
            new PropertyMetadata(null));

    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(
            nameof(Data), typeof(Geometry), typeof(Icon),
            new PropertyMetadata(null));

    public string Source
    {
        get => (string)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public Geometry Data
    {
        get => (Geometry)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.Property == SourceProperty && Source is string path)
        {
            LoadSvgData(path);
        }
    }

    private void LoadSvgData(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            string? xmlContent = null;
            string cleanPath = path.TrimStart('/');

            // 1. Intento principal: Formato explícito con el nombre del ensamblado (Resuelve en Diseñador y en Runtime)
            string? selfName = typeof(Icon).Assembly.GetName().Name;
            xmlContent = TryReadPack($"pack://application:,,,/{selfName};component/{cleanPath}");

            // 2. Fallback (Diseñador): Sin ensamblado explícito el pack apunta al proceso de diseño, no al app.
            //    El designer carga el ensamblado del app, así que lo buscamos entre los ensamblados cargados (igual que LangRX).
            if (xmlContent == null)
            {
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    string? name = asm.GetName().Name;

                    if (name == null || name == selfName ||
                        name.StartsWith("System") || name.StartsWith("Microsoft") || name.StartsWith("mscorlib"))
                        continue;

                    xmlContent = TryReadPack($"pack://application:,,,/{name};component/{cleanPath}");
                    if (xmlContent != null) break;
                }
            }

            // 3. Último recurso: Formato estándar de ejecución (Si el icono está en el ejecutable principal y no en el proyecto de la librería)
            if (xmlContent == null)
                xmlContent = TryReadPack($"pack://application:,,,/{cleanPath}");

            // 4. Procesar y asignar los datos del SVG
            if (!string.IsNullOrEmpty(xmlContent))
            {
                XDocument doc = XDocument.Parse(xmlContent);

                var pathElements = doc.Descendants()
                                      .Where(e => e.Name.LocalName.Equals("path", StringComparison.OrdinalIgnoreCase))
                                      .Select(p => p.Attribute("d")?.Value)
                                      .Where(d => !string.IsNullOrWhiteSpace(d));

                string combinedData = string.Join(" ", pathElements);

                if (!string.IsNullOrWhiteSpace(combinedData))
                {
                    Data = Geometry.Parse(combinedData);
                }
            }
        }
        catch
        {
            // En caso de error, la propiedad Data mantendrá el fallback del DependencyProperty
        }
    }

    private static string? TryReadPack(string packUri)
    {
        try
        {
            var streamInfo = Application.GetResourceStream(new Uri(packUri, UriKind.Absolute));
            if (streamInfo != null)
            {
                using var reader = new StreamReader(streamInfo.Stream);
                return reader.ReadToEnd();
            }
        }
        catch
        {
            // Ignorar: se prueba el siguiente intento de resolución
        }

        return null;
    }
}