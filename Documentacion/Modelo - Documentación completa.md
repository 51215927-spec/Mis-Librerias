# Modelo — Documentación completa

Guía de `ConstructorModelo`, `Modelo`, `Tabla<T>` y `ListaRelacionada<T>`. Reemplaza a `ORM` y a los atributos: `Atributos.cs` y `ORM.cs` se eliminan del proyecto.

> **Estado de verificación:** el código se compiló con .NET SDK 8.0.131 en Linux, sin advertencias, y se ejecutó un programa de prueba con los casos de la sección 9. No se probó en Windows, con concurrencia, con archivos grandes ni dentro de un proyecto real.

# Índice

1. [Idea general](#1-idea-general)
2. [Requisitos y archivos](#2-requisitos-y-archivos)
3. [Configurar una entidad](#3-configurar-una-entidad)
4. [Configurar relaciones](#4-configurar-relaciones)
5. [Construir y usar el modelo](#5-construir-y-usar-el-modelo)
6. [Claves foráneas: quién las modifica](#6-claves-foráneas-quién-las-modifica)
7. [Errores](#7-errores)
8. [Decisiones y limitaciones](#8-decisiones-y-limitaciones)
9. [Casos probados](#9-casos-probados)

---

# 1. Idea general

```text
Clase (IEntidad<T>.Configurar)  →  ConstructorModelo.Agregar<T>()
                                          │
                                   Construir()  ← valida todo
                                          │
                                        Modelo  ── CargarTodo / GuardarTodo ── CSV
                                          │
                              Uno<T>() / Muchos<T>()  ← desde las propiedades de navegación
```

- La configuración (archivo, columnas, relaciones) se escribe con **expresiones lambda** (`v => v.ClienteId`), no con textos ni atributos.
- Cada entidad se configura **dentro de su propia clase**, por lo que la lambda puede apuntar a campos y propiedades privados.
- `Construir()` valida todo una sola vez: los errores de configuración aparecen al iniciar el programa.
- El `Modelo` guarda las tablas cargadas y resuelve las relaciones.

# 2. Requisitos y archivos

| Requisito | Detalle |
| --- | --- |
| .NET | 8 o superior (usa miembros abstractos estáticos en interfaces). |
| Nullable | Pensado para `<Nullable>enable</Nullable>`. |

| Archivo | Contenido |
| --- | --- |
| `ConstructorModelo.cs` | `IEntidad<T>`, `ConstructorModelo`, `ConstructorEntidad<T>`, `ConstructorRelacion<TOrigen,TDestino>`. |
| `Modelo.cs` | `Modelo` y las piezas internas ya validadas. |
| `Tabla.cs` | `Tabla<T>` y `ListaRelacionada<T>`. |
| `CsvManager.cs` | Lectura y escritura. Ahora es `internal`: se usa solo a través del `Modelo`. |

# 3. Configurar una entidad

La clase implementa `IEntidad<T>` y define un método estático `Configurar`:

```csharp
using DataBases;

public class Cliente : IEntidad<Cliente>
{
    private int id;
    private string nombre;

    public Cliente(int id, string nombre) { this.id = id; this.nombre = nombre; }

    public int Id => id;
    public string Nombre => nombre;

    public static void Configurar(ConstructorEntidad<Cliente> e)
    {
        e.Archivo("datos/clientes.csv")
         .Columna(c => c.id)                  // columna "id"
         .Columna(c => c.nombre, "Nombre");   // columna con otro nombre
    }
}
```

| Método | Función |
| --- | --- |
| `Archivo(ruta)` | Ruta del CSV. Obligatorio. |
| `Columna(m => m.Miembro, nombre?)` | Miembro que se guarda. Si no se da `nombre`, la columna se llama como el miembro. |

Reglas:

1. El miembro puede ser un **campo o una propiedad**, de cualquier visibilidad. La lambda debe ser un acceso directo (`x => x.Id`); `x => x.Id + 1` lanza `CsvConfiguracionException`.
2. Debe existir un **constructor** con un parámetro por cada columna, con el mismo nombre (sin distinguir mayúsculas) y el mismo tipo que el miembro. Se valida en `Construir()`.
3. Tipos soportados: texto, `char`, `bool`, números, fechas y horas, `Guid`, enums y sus versiones `?`.
4. Las columnas de una clase base se declaran explícitamente en la clase derivada (`.Columna(p => p.Id)`).
5. Dos entidades no pueden usar el mismo archivo.

# 4. Configurar relaciones

```csharp
public class Venta : IEntidad<Venta>
{
    public int Id { get; }
    public int? ClienteId { get; set; }

    public Venta(int id, int? clienteId) { Id = id; ClienteId = clienteId; }

    public Cliente? Cliente
    {
        get => Datos.Modelo.Uno<Cliente>(this);
        set => ClienteId = value?.Id;
    }

    public static void Configurar(ConstructorEntidad<Venta> e)
    {
        e.Archivo("datos/ventas.csv").Columna(v => v.Id).Columna(v => v.ClienteId);

        e.Uno(v => v.Cliente)            // propiedad de navegación
         .Con(v => v.ClienteId)          // clave de esta clase
         .ApuntaA(c => c.Id);            // clave del destino
    }
}
```

## 4.1 Las cuatro formas

| Forma | Configuración |
| --- | --- |
| 1 → 1 directa | `e.Uno(x => x.Nav).Con(x => x.Clave).ApuntaA(d => d.Clave)` |
| 1 → N directa | `e.Muchos(x => x.Nav).Con(x => x.Clave).ApuntaA(d => d.Clave)` |
| 1 → 1 con tabla | `e.Uno(x => x.Nav).Con(...).Por<T>(t => t.haciaOrigen, t => t.haciaDestino).ApuntaA(...)` |
| 1 → N con tabla | `e.Muchos(x => x.Nav).Con(...).Por<T>(t => t.haciaOrigen, t => t.haciaDestino).ApuntaA(...)` |

Con tabla intermedia:

```csharp
e.Muchos(a => a.Materias)
 .Con(a => a.Id)
 .Por<AlumnoMateria>(t => t.AlumnoId, t => t.MateriaId)
 .ApuntaA(m => m.Id);
```

`AlumnoMateria` también debe agregarse al modelo (`Agregar<AlumnoMateria>()`), porque es una tabla con su propio CSV.

## 4.2 La propiedad de navegación

```csharp
// 1 → 1
public Cliente? Cliente => Datos.Modelo.Uno<Cliente>(this);

// 1 → N
public ListaRelacionada<Venta> Ventas => Datos.Modelo.Muchos<Venta>(this);
```

El nombre de la propiedad lo aporta el compilador (`[CallerMemberName]`): no se escribe ningún texto. El tipo de la propiedad 1 → N puede ser `ListaRelacionada<T>`, `IList<T>`, `ICollection<T>` o `IEnumerable<T>`.

## 4.3 Resultados

| Situación | `Uno<T>` | `Muchos<T>` |
| --- | --- | --- |
| Clave del objeto `null` | `null` | lista vacía |
| Sin coincidencias | `null` | lista vacía |
| Una coincidencia | el objeto | lista de 1 |
| Varias coincidencias | `InvalidOperationException` | lista de N |

Las claves `int` e `int?` son compatibles entre sí. Un `null` nunca relaciona objetos. Las filas repetidas en una tabla intermedia no producen duplicados, y el resultado sale en el orden de la tabla destino.

# 5. Construir y usar el modelo

```csharp
public static class Datos
{
    public static Modelo Modelo { get; private set; } = null!;

    public static void Iniciar()
    {
        Modelo = new ConstructorModelo()
            .Agregar<Cliente>()
            .Agregar<Venta>()
            .Construir();        // valida todo; falla aquí si algo está mal

        Modelo.CargarTodo();     // archivos inexistentes → tablas vacías
    }
}
```

| Método de `Modelo` | Función |
| --- | --- |
| `Cargar<T>(exigirArchivo = false)` | Carga una tabla. Con `true`, lanza `FileNotFoundException` si falta el archivo. |
| `CargarTodo()` / `GuardarTodo()` | Todas las entidades. |
| `Guardar<T>()` | Guarda la tabla de `T`. |
| `Tabla<T>()` | Devuelve la `Tabla<T>` cargada. |
| `Uno<T>(objeto)` / `Muchos<T>(objeto)` | Resuelven relaciones; se llaman desde la propiedad de navegación. |

`Tabla<T>` hereda de `Collection<T>`: `Add`, `Remove`, índice, `Count`, `foreach` y LINQ. Agrega `AgregarRango`. No admite elementos `null`.

```csharp
Datos.Modelo.Tabla<Venta>().Add(new Venta(1, 3));
Datos.Modelo.Guardar<Venta>();
```

# 6. Claves foráneas: quién las modifica

**El programador.** El modelo no modifica claves por su cuenta, porque un campo o una propiedad automática no avisa cuando cambia. Hay tres lugares donde se escribe esa lógica.

**a) `set` de la propiedad de navegación (1 → 1):**

```csharp
set => ClienteId = value?.Id;
```

**b) Bloques `AlAgregar` y `AlQuitar` (1 → N):**

```csharp
e.Muchos(c => c.Ventas).Con(c => c.id).ApuntaA(v => v.ClienteId)
 .AlAgregar((c, v) => v.ClienteId = c.id)
 .AlQuitar((c, v) => v.ClienteId = null);
```

`cliente.Ventas.Add(venta)` ejecuta `AlAgregar` y luego agrega a la lista; `Remove` y `Clear` ejecutan `AlQuitar`. Los bloques se escriben dentro de `Configurar`, por lo que pueden asignar miembros privados.

**c) Tablas intermedias:** el bloque crea o elimina la fila intermedia.

```csharp
.AlAgregar((a, m) => Datos.Modelo.Tabla<AlumnoMateria>().Add(new AlumnoMateria(a.Id, m.Id)))
```

Reglas importantes:

- `ListaRelacionada<T>` es una **copia** del resultado en el momento de la consulta. Si la tabla cambia después, la lista no se actualiza; hay que volver a pedirla.
- Sin `AlAgregar`/`AlQuitar`, `Add`, `Remove`, `Clear` y el reemplazo por índice lanzan `NotSupportedException`. Es intencional: una lista que acepta `Add` y no cambia nada engaña.
- `AlAgregar` **no** agrega el objeto a su tabla (`Tabla<Venta>`). Si el objeto es nuevo, también hay que agregarlo a la tabla.
- `Muchos<T>` crea una lista nueva en cada acceso. Dentro de un bucle conviene guardarla en una variable local.

# 7. Errores

## 7.1 `CsvConfiguracionException` (al llamar a `Agregar<T>()` o `Construir()`)

| Mensaje (resumido) | Causa |
| --- | --- |
| `La entidad X no indica su archivo.` | Falta `.Archivo(...)`. |
| `La entidad X no declara ninguna columna.` | Falta `.Columna(...)`. |
| `la expresión '...' debe ser un acceso directo...` | La lambda no es `x => x.Miembro`. |
| `El tipo T del miembro X.m no está soportado.` | Lista, arreglo, objeto, etc. como columna. |
| `X tiene nombres de columna repetidos` / `declara el mismo miembro en más de una columna.` | Duplicados. |
| `No se encontró un constructor compatible para X...` | Ningún constructor coincide con las columnas. |
| `Hay entidades que usan el mismo archivo` | Dos entidades con la misma ruta. |
| `X.Nav: falta .Con(...)` / `falta .ApuntaA(...)` | Relación incompleta. |
| `apunta a D, que no está agregada con Agregar<D>()` | Destino o tabla intermedia sin registrar. |
| `los miembros no tienen tipos compatibles` | Claves de distinto tipo (se permite `int` con `int?`). |
| `la propiedad es de tipo ...` | La propiedad de navegación no coincide con la relación. |
| `AlAgregar y AlQuitar solo valen en relaciones 1→N.` | Bloques en una relación 1 → 1. |
| `X ya tiene una relación llamada 'N'` / `X ya fue agregada al modelo.` | Duplicados. |

## 7.2 En ejecución

| Excepción | Cuándo |
| --- | --- |
| `InvalidOperationException` | Tabla sin cargar; entidad o relación no definida en el modelo; `Uno` usado sobre una relación `Muchos` (o al revés) o con otro tipo destino; varias coincidencias en 1 → 1. |
| `NotSupportedException` | Modificar una `ListaRelacionada` sin `AlAgregar`/`AlQuitar`. |
| `CsvFormatException` | Contenido inválido del CSV (con línea y columna). |
| `FileNotFoundException` | `Cargar<T>(exigirArchivo: true)` sin archivo. |

# 8. Decisiones y limitaciones

| Tema | Detalle |
| --- | --- |
| Sin índices | Cada acceso recorre la tabla destino completa: O(m) en relaciones directas, O(n + m) con tabla intermedia. Un índice se desactualizaría cuando el programador cambia una FK, porque no hay aviso de cambio. |
| Sin caché de navegación | Cada acceso resuelve de nuevo. Si se guarda el resultado en un campo, ese valor puede quedar desactualizado. |
| Sin sincronización entre hilos | `Modelo` y `Tabla<T>` no son seguros entre hilos. |
| Sin transacciones | Guardar varias tablas no es atómico en conjunto (cada archivo sí se escribe de forma atómica). |
| `List<T>` | `List<T>.Add` no es virtual y no se puede interceptar; por eso las colecciones heredan de `Collection<T>`. No tienen `Sort` ni `Find`; se usa LINQ o `.ToList()`. |
| Herencia | `Uno` y `Muchos` buscan la relación en el tipo real del objeto y luego en sus bases. Una clase derivada que también implementa `IEntidad` declara su propio `Configurar` (con `new` si oculta el de la base). |
| Texto en la configuración | Solo queda en rutas de archivo y nombres de columna opcionales. |

# 9. Casos probados

Ejecutados con un programa de prueba sobre .NET 8: 1 → N directa; 1 → 1 directa; FK `null`; `Add` y `Remove` con `AlAgregar`/`AlQuitar`; `set` de navegación; 1 → N con tabla (sin duplicados y en orden); `Add` sin `AlAgregar` (`NotSupportedException`); guardado y recarga (texto con comillas, FK modificada, relación con tabla); destino no registrado; relación inexistente; herencia con campo privado en la base; lambda inválida (`x => x.Id + 1`).

**No probados:** `Uno` con tabla intermedia, claves `string` o `Guid`, varias coincidencias en 1 → 1, concurrencia, archivos grandes, Windows.
