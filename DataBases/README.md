# DataBases

Biblioteca .NET 8 para guardar objetos en archivos CSV y resolver relaciones en memoria mediante un modelo configurado por cada entidad. `Modelo`, `ConstructorModelo`, `Tabla<T>` y `ListaRelacionada<T>` forman la API pública; `CsvManager` es interno. No depende de Utilidades.

- [Instalación y ejemplo completo](#instalación-y-ejemplo-completo)
- [Modelo y relaciones](#modelo-y-relaciones)
- [Lectura y escritura CSV](#lectura-y-escritura-csv)

## Verificación y alcance

Revisión del 03/11/2026: se compilan las bibliotecas en Windows con SDK 10.0.401, destino net8.0, y el ejemplo completo de abajo se ejecuta sobre .NET 8.0.33 en una carpeta temporal. Los apartados de casos registrados conservan los antecedentes de las guías anteriores; no son una nueva comprobación de toda la persistencia, concurrencia o sistemas operativos.

`GuardarTodo` y `CargarTodo` bloquean el conjunto de archivos, pero no son transacciones: un fallo durante GuardarTodo puede dejar archivos ya publicados y uno durante CargarTodo puede dejar tablas anteriores del recorrido ya recargadas. Si la aplicación necesita reemplazo integral sin pérdidas, debe preparar y validar una copia temporal antes de reemplazar su estado. `Cargar<T>` sí conserva la tabla anterior si la lectura de ese archivo falla.

## Instalación y ejemplo completo

Desde la raíz del repositorio de bibliotecas:

```powershell
dotnet build DataBases/DataBases.csproj
```

La solución `Mis Librerias.slnx` requiere un SDK que admita ese formato; alternativamente compilar cada proyecto. En una aplicación compatible agregar la referencia al proyecto DataBases.csproj; no copiar sus archivos ni referenciar la carpeta DataBases viejo, que no forma parte de la solución actual.

El siguiente programa independiente guarda y recarga una nota. Sus archivos se crean en `datos/`, relativa al directorio de trabajo al configurar la entidad:

```csharp
using DataBases;

var modelo = new ConstructorModelo().Agregar<Nota>().Construir();
modelo.Cargar<Nota>();
modelo.Tabla<Nota>().Add(new Nota(1, "Primera nota", 8.5m));
modelo.Guardar<Nota>();
modelo.Cargar<Nota>();
Console.WriteLine($"Notas cargadas: {modelo.Tabla<Nota>().Count}");

public class Nota : IEntidad<Nota>
{
    public int Id { get; }
    public string Texto { get; }
    public decimal Valoracion { get; }

    public Nota(int id, string texto, decimal valoracion)
    {
        Id = id;
        Texto = texto;
        Valoracion = valoracion;
    }

    public static void Configurar(ConstructorEntidad<Nota> entidad)
    {
        entidad.Archivo("datos/notas.csv")
            .Columna(n => n.Id)
            .Columna(n => n.Texto)
            .Columna(n => n.Valoracion);
    }
}
```

Cada ejecución agrega otra nota. La biblioteca no impone por sí sola unicidad de identificadores ni valida las reglas del dominio; la aplicación es responsable de esas decisiones. Los nombres y tipos de los parámetros del constructor deben coincidir con los miembros configurados, sin distinguir mayúsculas en los nombres.

Los ejemplos de las secciones siguientes son fragmentos independientes; no se concatenan con el programa anterior.

## Modelo y relaciones

Guía de `ConstructorModelo`, `Modelo`, `Tabla<T>` y `ListaRelacionada<T>`. Reemplaza a `ORM` y a los atributos: `Atributos.cs` y `ORM.cs` no pertenecen al proyecto actual; sus versiones históricas quedan en `../DataBases viejo/`.


### Índice de modelo

1. [Idea general](#1-idea-general)
2. [Requisitos y archivos](#2-requisitos-y-archivos)
3. [Configurar una entidad](#3-configurar-una-entidad)
4. [Configurar relaciones](#4-configurar-relaciones)
5. [Construir y usar el modelo](#5-construir-y-usar-el-modelo)
6. [Claves foráneas: quién las modifica](#6-claves-foráneas-quién-las-modifica)
7. [Errores](#7-errores)
8. [Decisiones y limitaciones](#8-decisiones-y-limitaciones)
9. [Casos registrados en la guía anterior](#9-casos-registrados-en-la-guía-anterior)

---

### 1. Idea general

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

### 2. Requisitos y archivos

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

### 3. Configurar una entidad

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
| `Archivo(ruta)` | Ruta del CSV. Obligatorio. Una ruta relativa se convierte a absoluta en ese momento, contra el directorio de trabajo actual. |
| `Columna(m => m.Miembro, nombre?)` | Miembro que se guarda. Si no se da `nombre`, la columna se llama como el miembro. |

Reglas:

1. El miembro puede ser un **campo o una propiedad**, de cualquier visibilidad. La lambda debe ser un acceso directo (`x => x.Id`); `x => x.Id + 1` lanza `CsvConfiguracionException`.
2. Debe existir un **constructor** con un parámetro por cada columna, con el mismo nombre (sin distinguir mayúsculas) y el mismo tipo que el miembro. Se valida en `Construir()`.
3. Tipos soportados: texto, `char`, `bool`, números, fechas y horas, `Guid`, enums y sus versiones `?`.
4. Las columnas de una clase base se declaran explícitamente en la clase derivada (`.Columna(p => p.Id)`).
5. Dos entidades no pueden usar el mismo archivo. La comparación distingue mayúsculas en Linux y no en Windows.
6. Si varios programas comparten un archivo, todos deben indicar la misma ruta absoluta.

### 4. Configurar relaciones

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

#### 4.1 Las cuatro formas

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

#### 4.2 La propiedad de navegación

```csharp
// 1 → 1
public Cliente? Cliente => Datos.Modelo.Uno<Cliente>(this);

// 1 → N
public ListaRelacionada<Venta> Ventas => Datos.Modelo.Muchos<Venta>(this);
```

El nombre de la propiedad lo aporta el compilador (`[CallerMemberName]`): no se escribe ningún texto. El tipo de la propiedad 1 → N puede ser `ListaRelacionada<T>`, `IList<T>`, `ICollection<T>` o `IEnumerable<T>`.

#### 4.3 Resultados

| Situación | `Uno<T>` | `Muchos<T>` |
| --- | --- | --- |
| Clave del objeto `null` | `null` | lista vacía |
| Sin coincidencias | `null` | lista vacía |
| Una coincidencia | el objeto | lista de 1 |
| Varias coincidencias | `InvalidOperationException` | lista de N |

Las claves `int` e `int?` son compatibles entre sí. Un `null` nunca relaciona objetos. Las filas repetidas en una tabla intermedia no producen duplicados, y el resultado sale en el orden de la tabla destino.

### 5. Construir y usar el modelo

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
| `Cargar<T>(exigirArchivo = false)` | Carga una tabla. Con `true`, lanza `FileNotFoundException` si falta el archivo. Si la tabla ya estaba cargada, conserva la misma instancia de `Tabla<T>` (se vacía y se vuelve a llenar); si la lectura falla, la tabla anterior no cambia. |
| `CargarTodo()` / `GuardarTodo()` | Todas las entidades, con todos sus archivos bloqueados a la vez (ver la sección 2.3 de CSV en este README). |
| `Guardar<T>()` | Guarda la tabla de `T`. Lanza `CsvConcurrenciaException` si otro proceso modificó el archivo después de cargarlo. |
| `Tabla<T>()` | Devuelve la `Tabla<T>` cargada. |
| `Uno<T>(objeto)` / `Muchos<T>(objeto)` | Resuelven relaciones; se llaman desde la propiedad de navegación. |

`Tabla<T>` hereda de `Collection<T>`: `Add`, `Remove`, índice, `Count`, `foreach` y LINQ. Agrega `AgregarRango`. No admite elementos `null`.

```csharp
Datos.Modelo.Tabla<Venta>().Add(new Venta(1, 3));
Datos.Modelo.Guardar<Venta>();
```

### 6. Claves foráneas: quién las modifica

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

### 7. Errores

#### 7.1 `CsvConfiguracionException` (al llamar a `Agregar<T>()` o `Construir()`)

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

#### 7.2 En ejecución

| Excepción | Cuándo |
| --- | --- |
| `InvalidOperationException` | Tabla sin cargar; entidad o relación no definida en el modelo; `Uno` usado sobre una relación `Muchos` (o al revés) o con otro tipo destino; varias coincidencias en 1 → 1. |
| `NotSupportedException` | Modificar una `ListaRelacionada` sin `AlAgregar`/`AlQuitar`. |
| `CsvFormatException` | Contenido inválido del CSV (con línea y columna). |
| `CsvConcurrenciaException` | `Guardar<T>()` o `GuardarTodo()` con un archivo modificado por otro proceso desde la última carga. Se recarga y se repiten los cambios. |
| `IOException` | Otro proceso mantuvo bloqueado el archivo más de 10 segundos. |
| `FileNotFoundException` | `Cargar<T>(exigirArchivo: true)` sin archivo. |

### 8. Decisiones y limitaciones

| Tema | Detalle |
| --- | --- |
| Sin índices | Cada acceso recorre la tabla destino completa: O(m) en relaciones directas, O(n + m) con tabla intermedia. Un índice se desactualizaría cuando el programador cambia una FK, porque no hay aviso de cambio. |
| Sin caché de navegación | Cada acceso resuelve de nuevo. Si se guarda el resultado en un campo, ese valor puede quedar desactualizado. |
| Sin sincronización entre hilos | `Modelo` y `Tabla<T>` no son seguros entre hilos. |
| Sin transacciones | `GuardarTodo` bloquea todos los archivos juntos, pero no es una transacción: si falla a mitad, los archivos ya escritos quedan guardados (cada archivo sí se escribe de forma atómica). |
| Varios procesos | Los bloqueos y la detección de cambios protegen a los programas que usan esta biblioteca. Una edición externa se detecta al guardar, no se impide. Los datos en memoria de cada proceso no se actualizan solos: hay que llamar a `Cargar`. |
| `List<T>` | `List<T>.Add` no es virtual y no se puede interceptar; por eso las colecciones heredan de `Collection<T>`. No tienen `Sort` ni `Find`; se usa LINQ o `.ToList()`. |
| Herencia | `Uno` y `Muchos` buscan la relación en el tipo real del objeto y luego en sus bases. Una clase derivada que también implementa `IEntidad` declara su propio `Configurar` (con `new` si oculta el de la base). |
| Texto en la configuración | Solo queda en rutas de archivo y nombres de columna opcionales. |

### 9. Casos registrados en la guía anterior

La guía anterior registraba estos ensayos sobre .NET 8 (no se reejecutan en esta reorganización): 1 → N directa; 1 → 1 directa; FK `null`; `Add` y `Remove` con `AlAgregar`/`AlQuitar`; `set` de navegación; 1 → N con tabla (sin duplicados y en orden); `Add` sin `AlAgregar` (`NotSupportedException`); guardado y recarga (texto con comillas, FK modificada, relación con tabla); destino no registrado; relación inexistente; herencia con campo privado en la base; lambda inválida (`x => x.Id + 1`).

También se probó (Linux): `Cargar` conserva la instancia de `Tabla<T>`, `CsvConcurrenciaException` tras una modificación externa y cuatro procesos escribiendo el mismo archivo sin perder filas.

**Sin cobertura en ese registro anterior:** `Uno` con tabla intermedia, claves `string` o `Guid`, varias coincidencias en 1 → 1, concurrencia entre hilos, archivos grandes, Windows, carpetas de red.

## Lectura y escritura CSV

Guía de `CsvManager`, la clase interna que guarda y lee tablas en archivos CSV.


### Índice de CSV

1. [Qué cambió](#1-qué-cambió)
2. [Cómo se usa ahora](#2-cómo-se-usa-ahora)
3. [El formato del archivo CSV](#3-el-formato-del-archivo-csv)
4. [Errores](#4-errores)
5. [Funcionamiento interno](#5-funcionamiento-interno)
6. [Decisiones y limitaciones](#6-decisiones-y-limitaciones)
7. [Casos registrados en la guía anterior](#7-casos-registrados-en-la-guía-anterior)

---

### 1. Qué cambió

| Antes | Ahora |
| --- | --- |
| Clase `public static CsvManager` con `Write`, `Read` y `ReadOrEmpty`. | Clase `internal`. Se usa a través de `Modelo.Cargar`, `CargarTodo`, `Guardar` y `GuardarTodo`. |
| `[Atributos.Columna("x")]` sobre campos. | `.Columna(c => c.Miembro, "x")` en `Configurar` (ver la sección Modelo y relaciones de este README). |
| Solo campos. | Campos **o propiedades**, de cualquier visibilidad. |
| La ruta se pasaba en cada llamada. | `.Archivo("ruta.csv")`, una sola vez por entidad. |
| `Read` lanzaba si faltaba el archivo y `ReadOrEmpty` devolvía lista vacía. | `Cargar<T>(exigirArchivo: false)` devuelve tabla vacía; con `true` lanza `FileNotFoundException`. |
| La clase se validaba en cada llamada. | Se valida una vez en `ConstructorModelo.Construir()`. |
| Columnas heredadas detectadas por atributo. | Se declaran explícitamente en la clase derivada. |

`CsvException`, `CsvFormatException` y `CsvConfiguracionException` siguen siendo públicas y no cambiaron. Se agrega `CsvConcurrenciaException` (sección 4).

El formato del archivo (sección 3), el análisis de líneas y la conversión de valores **no cambiaron**. Cambiaron la escritura (archivo temporal con nombre único, vaciado a disco, comprobación de cambios externos) y el bloqueo (ahora también entre procesos; ver 2.3).

### 2. Cómo se usa ahora

```csharp
Datos.Modelo.CargarTodo();                         // lee todos los CSV
Datos.Modelo.Tabla<Persona>().Add(new Persona(...));
Datos.Modelo.Guardar<Persona>();                   // reescribe el CSV completo de Persona
```

#### 2.1 Reglas para la clase

| # | Regla |
| --- | --- |
| 1 | Cada dato guardado se declara con `.Columna(x => x.Miembro)`; el miembro es un campo o una propiedad legible. |
| 2 | Debe existir un constructor con un parámetro por columna, con el mismo nombre (sin distinguir mayúsculas) y el mismo tipo que el miembro. |
| 3 | Los tipos son los soportados: texto, `char`, `bool`, números, `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan`, `Guid`, enums y sus versiones `?`. |
| 4 | Los nombres de columna no pueden estar vacíos ni contener `,` `"` `\` ni saltos de línea, y no pueden repetirse. |

Una propiedad calculada (por ejemplo `public int Doble => id * 2;`) se podría declarar como columna y se escribiría, pero la clase no tendría un constructor compatible a menos que haya un parámetro `doble`. Conviene declarar solo datos reales.

#### 2.2 Escritura y lectura

| Aspecto | Comportamiento |
| --- | --- |
| Archivo existente | Se reemplaza por completo con el contenido de la tabla. |
| Carpeta inexistente | Se crea. |
| Tabla vacía | Se escribe solo el encabezado. |
| Valor `null` | Se escribe la palabra `null` sin comillas. |
| Enum sin `[Flags]` con un valor no definido | `ArgumentException` al guardar. |
| Corte durante el guardado | El archivo anterior queda intacto (archivo temporal + reemplazo). Antes del reemplazo se fuerza la escritura a disco. |
| Archivo modificado por otro proceso (o a mano) después de cargar | `CsvConcurrenciaException` al guardar. Hay que volver a cargar la tabla. |
| Ruta relativa | Se convierte a absoluta al llamar a `.Archivo(...)`, contra el directorio de trabajo de ese momento. |
| Codificación | UTF-8 sin BOM. |
| Archivo vacío o solo encabezado | Tabla vacía. |
| Orden de las columnas al leer | No importa: se asocian por nombre. |
| Líneas en blanco | Se ignoran. |

`Guardar` escribe **toda** la tabla cada vez.

#### 2.3 Varios procesos sobre el mismo archivo

- Cada archivo `datos.csv` tiene un archivo de bloqueo `datos.csv.lock` en su carpeta. Se crea solo y no se borra; se puede ignorar (por ejemplo, en el control de versiones).
- `Leer` y `Escribir` toman ese bloqueo. Si otro proceso lo tiene, esperan hasta 10 segundos y luego lanzan `IOException`.
- `CargarTodo` y `GuardarTodo` (en el `Modelo`) toman los bloqueos de **todos** los archivos juntos, para que otro proceso no lea un conjunto a medio guardar.
- Al cargar o guardar, la tabla recuerda la fecha de modificación y el tamaño del archivo. Si al guardar el archivo es distinto, se lanza `CsvConcurrenciaException` y no se escribe nada.
- Quien reciba `CsvConcurrenciaException` debe volver a cargar y repetir sus cambios. Un patrón posible:

```csharp
while (true)
{
    Datos.Modelo.Cargar<Persona>();
    Datos.Modelo.Tabla<Persona>().Add(new Persona(...));

    try { Datos.Modelo.Guardar<Persona>(); break; }
    catch (CsvConcurrenciaException) { /* otro proceso guardó antes: se reintenta */ }
}
```

- Para que dos programas compartan un archivo deben indicar la **misma ruta absoluta**. Una ruta relativa se resuelve contra el directorio de trabajo de cada programa.

### 3. El formato del archivo CSV

`CsvManager` usa un formato propio, **inspirado en CSV pero no idéntico al estándar**. Conviene conocerlo para editar archivos a mano o depurar.

#### 3.1 Estructura

```text
id,nombre,email,nacimiento,estado
1,"Ana Pérez","ana@mail.com",1990-05-17,"Activo"
2,"Luis",null,2001-12-03,"Inactivo"
```

- **Línea 1:** encabezado con los nombres de columna definidos con `.Columna(...)`.
- **Líneas siguientes:** un objeto por línea.
- **Separador:** coma `,`.
- **Orden de columnas al leer:** no importa; se asocian por nombre.
- **Líneas en blanco:** se ignoran.

#### 3.2 Cómo se escribe cada tipo

| Tipo | Ejemplo en el archivo |
| --- | --- |
| `string` | `"Ana Pérez"` (siempre entre comillas) |
| `char` | `"x"` (entre comillas) |
| `enum` | `"Activo"` (entre comillas, se guarda el **nombre**) |
| `int`, `long`, etc. | `42` |
| `double`, `decimal` | `3.5` (siempre con **punto**) |
| `bool` | `True` o `False` |
| `DateTime` | `2026-10-01T14:30:00.0000000` |
| `DateOnly` | `2026-10-01` |
| `TimeOnly` | `14:30:00.0000000` |
| `TimeSpan` | `1.02:03:04` (días.horas:minutos:segundos) |
| `Guid` | `3f2504e0-4f89-11d3-9a0c-0305e82c3301` |
| `null` | `null` (la palabra, sin comillas) |

#### 3.3 `null` frente a texto vacío

| En el archivo | Significa |
| --- | --- |
| `null` (sin comillas) | `null` |
| `""` (comillas vacías) | Texto vacío `""` |
| `"null"` (con comillas) | El texto `null` |
| Celda vacía sin comillas (`,,`) | `null` (solo al leer; `Escribir` nunca la produce) |

Reglas:

- La palabra `null` se reconoce solo en **minúscula** y **sin comillas**. `Null` o `NULL` sin comillas no son `null`: se intentan convertir al tipo del campo y fallan (o, en un `string`, se leen como texto).
- Esta diferencia solo funciona si el archivo **conserva las comillas**. Si una herramienta las quita, `""` se leerá como `null` y `"null"` también.
- La celda vacía sin comillas se sigue aceptando como `null` para que los archivos escritos con versiones anteriores se lean sin cambios. Al volver a guardarlos quedan con la palabra `null`.

#### 3.4 Caracteres especiales dentro de textos

| Carácter | Se escribe |
| --- | --- |
| `\` | `\\` |
| `"` | `\"` |
| Salto de línea | `\n` |
| Retorno de carro | `\r` |
| Tabulación | `\t` |

Ejemplo: el texto `Dijo "hola"` seguido de un salto de línea se guarda como:

```text
"Dijo \"hola\"\n"
```

Así, **cada registro ocupa siempre una sola línea del archivo**, aunque el texto original tenga varios renglones.

#### 3.5 Diferencias con el CSV estándar

| Aspecto | CSV estándar (RFC 4180) | CsvManager |
| --- | --- | --- |
| Comilla dentro de un texto | Se duplica: `""` | Se escapa: `\"` |
| Saltos de línea en un texto | Permitidos dentro de comillas | Se escriben como `\n` |
| Separador | Varía según región (`,` o `;`) | Siempre `,` |
| Valor nulo | No existe; suele ser una celda vacía | La palabra `null` sin comillas |

Consecuencia: un archivo escrito por otro programa con comillas duplicadas (`""`) **no se lee correctamente**.

#### 3.6 Editar a mano

Reglas para no romper el archivo:

1. No cambiar el encabezado, salvo para reordenar columnas (se puede, siempre que estén todas).
2. Poner los textos entre comillas, sobre todo si contienen comas.
3. Usar punto decimal (`3.5`, no `3,5`).
4. Cada fila debe tener exactamente tantas celdas como el encabezado.
5. Respetar los formatos de fecha de la tabla 3.2.
6. Guardar en UTF-8.
7. Escribir los valores ausentes como `null` (minúscula, sin comillas).
8. Si el programa está abierto, editar el archivo hace que el siguiente `Guardar` falle con `CsvConcurrenciaException` (ver 2.3).

---

### 4. Errores

Jerarquía:

```text
Exception
 └── CsvException
      ├── CsvFormatException          ← el ARCHIVO tiene un problema (Linea, Columna)
      ├── CsvConcurrenciaException    ← otro proceso cambió el archivo desde que se cargó
      └── CsvConfiguracionException   ← la configuración de la entidad está mal
```

Los errores de entrada y salida (`IOException`, `UnauthorizedAccessException`) no se envuelven. Si otro proceso mantiene el bloqueo de un archivo más de 10 segundos, se lanza `IOException`.

#### 4.1 `CsvFormatException` — corregir el archivo

Todos comienzan con `Línea N:` o `Línea N, columna 'c':`.

| Mensaje | Causa |
| --- | --- |
| `Parece que el archivo usa ';' como separador...` | Archivo guardado con punto y coma (típico de Excel en español). |
| `Columnas duplicadas en el encabezado: ...` | Una columna aparece dos veces. |
| `Las columnas [...] no existen en X.` | El encabezado tiene nombres que la entidad no define. |
| `Faltan las columnas [...] requeridas por X.` | La entidad tiene columnas que no están en el archivo. |
| `Se esperaban N columnas pero hay M.` | Fila con más o menos celdas que el encabezado. |
| `La línea termina con una barra '\' incompleta.` | Una `\` al final de la línea. |
| `Hay comillas sin cerrar.` | Número impar de comillas. |
| `La celda es null pero X no admite null.` | `null` o celda vacía en un miembro no nulo. |
| `No se pudo convertir 'abc' a Int32: ...` | El texto no corresponde al tipo. |
| `'x' no es un valor válido de Estado. Valores: ...` | Valor de enum inexistente. |
| `El constructor de X rechazó los datos: ...` | El constructor lanzó una excepción propia. |

#### 4.2 `CsvConfiguracionException` — corregir el código

Los mensajes de configuración (columnas, tipos, constructor, archivo duplicado) están en la sección 7 de la documentación del Modelo. Se lanzan al llamar a `Construir()`.

#### 4.3 Al escribir

| Excepción | Causa |
| --- | --- |
| `ArgumentException`: `La tabla contiene un elemento null.` | Un elemento `null` (`Tabla<T>` ya los rechaza al agregar). |
| `ArgumentException`: `El valor V no está definido en el enum E...` | Enum sin `[Flags]` con un valor fuera de su lista. |
| `CsvConcurrenciaException`: `El archivo ... fue modificado después de cargar X...` | Otro proceso (o una edición a mano) cambió el archivo después de cargar la tabla. Recargar con `Cargar` y repetir los cambios. |

---

### 5. Funcionamiento interno

```text
CsvManager (internal)
├── Bloquear(rutas)                 bloqueo por archivo: Monitor (hilos) + archivo .lock (procesos)
├── Escribir<T>(entidad, datos)     bloqueo; comprueba versión; archivo temporal único; Flush(true); File.Move con reintentos
├── Leer<T>(entidad, exigir)        bloqueo; lee con FileShare.ReadWrite/Delete; ParsearTabla
├── ParsearTabla<T>(entidad, líneas) valida encabezado; BuscarConstructor una vez; una fila = un objeto
├── ValidarConstructor(entidad)     usado por Construir()
├── EsTipoSoportado(tipo)           usado por Construir()
├── BuscarConstructor(tipo, miembros)   empareja parámetros con miembros por nombre y tipo
├── FormatearValor / EscaparString  de valor a texto
└── ParsearLinea / ConvertirValor   de texto a valor
```

Lo que lee y escribe cada columna es un `Miembro` (campo o propiedad obtenido de la lambda): `Miembro.Leer(objeto)` usa `FieldInfo.GetValue` o `PropertyInfo.GetValue`, que funcionan con miembros privados.

La reconstrucción sigue usando el **constructor**, nunca los setters: por eso una propiedad `{ get; }` o `{ get; private set; }` sirve como columna.

El orden de los casos de `FormatearValor`, la lógica de `ParsearLinea` (banderas `escapado`, `dentroDeString`, `huboComillas`) y las reglas de `ConvertirValor` son las mismas que antes.

### 6. Decisiones y limitaciones

| Tema | Detalle |
| --- | --- |
| Bloqueo por archivo | Cada archivo tiene su propio bloqueo, también entre procesos (2.3). Solo protege a programas que usen esta biblioteca: un editor de texto, Excel u otro programa que escriba el CSV sin pasar por ella no respeta el bloqueo (se detecta al guardar, pero no se evita). |
| Carpetas de red | El bloqueo entre procesos depende del sistema de archivos. En recursos de red (SMB, NFS) puede no ser confiable. No se probó. |
| Pérdida de cambios al reintentar | `CsvConcurrenciaException` descarta los cambios en memoria si se recarga. La biblioteca no fusiona cambios: quien guarda decide cómo repetirlos. |
| Archivos grandes | `File.ReadAllLines` carga el archivo entero en memoria. |
| Sin transacciones | `GuardarTodo` bloquea todos los archivos juntos, pero si falla a mitad los ya escritos quedan guardados. |
| Formato propio | No es el CSV estándar de la RFC 4180 (ver 3.5). |
| Cambios de estructura | Renombrar o agregar columnas deja inservibles los archivos viejos. |
| Excel | Puede alterar el archivo al guardarlo (separador, comillas, codificación). Preferir un editor de texto. |
| Datos sensibles | El archivo es texto plano, sin cifrado. |
| Sin API pública | `CsvManager` ya no es público. Si se necesita usarlo sin `Modelo`, hay que volver a exponer `Leer` y `Escribir` y construir un `EntidadModelo` (que también es interno). |

### 7. Casos registrados en la guía anterior

La guía anterior registraba estos ensayos sobre .NET 8 (no se reejecutan en esta reorganización): guardado y recarga de texto con comillas escapadas, enteros, `int?` con valor, `decimal` con punto, propiedades `{ get; }` y `{ get; set; }`, campos privados, herencia (campo privado de la base), archivo inexistente (tabla vacía) y ejecución de `GuardarTodo` con cinco entidades. En la versión con bloqueo entre procesos (Linux, .NET 8): archivo inexistente, texto con comillas, barra, coma y salto de línea, recarga que conserva la misma instancia de tabla, ausencia de archivos `.tmp` sobrantes, `CsvConcurrenciaException` tras una modificación externa, y cuatro procesos agregando 25 filas cada uno sobre el mismo archivo (no se perdió ninguna fila; hubo reintentos por conflicto).

**Sin cobertura en ese registro anterior:** Windows (reemplazo de un archivo abierto por otro programa), carpetas de red, `null` escrito y leído en el CSV, columna con nombre distinto al del miembro, fechas, `Guid`, enums `[Flags]`, `char`, archivos con `;`, comillas sin cerrar, línea terminada en `\`, cantidad de columnas incorrecta y excepciones lanzadas por el constructor. Ese código no se modificó respecto de la versión anterior, pero no se volvió a ejecutar.

[Volver al repositorio](../README.md) · [Utilidades](../Utilidades/README.md)
