# CsvManager — Documentación completa

Guía de `CsvManager`, la clase interna que guarda y lee tablas en archivos CSV.

> **Estado de verificación:** el código se compiló con .NET SDK 8.0.131 en Linux, sin advertencias, y se probó junto con el `Modelo` (guardar, recargar, texto con comillas, decimales, FK modificada). No se probó en Windows, con archivos grandes ni con concurrencia. Los casos no mencionados en la sección 7 de este documento se mantienen como estaban descritos antes y no se volvieron a ejecutar.

# Índice

1. [Qué cambió](#1-qué-cambió)
2. [Cómo se usa ahora](#2-cómo-se-usa-ahora)
3. [El formato del archivo CSV](#3-el-formato-del-archivo-csv)
4. [Errores](#4-errores)
5. [Funcionamiento interno](#5-funcionamiento-interno)
6. [Decisiones y limitaciones](#6-decisiones-y-limitaciones)
7. [Casos probados](#7-casos-probados)

---

# 1. Qué cambió

| Antes | Ahora |
| --- | --- |
| Clase `public static CsvManager` con `Write`, `Read` y `ReadOrEmpty`. | Clase `internal`. Se usa a través de `Modelo.Cargar`, `CargarTodo`, `Guardar` y `GuardarTodo`. |
| `[Atributos.Columna("x")]` sobre campos. | `.Columna(c => c.Miembro, "x")` en `Configurar` (ver la documentación del Modelo). |
| Solo campos. | Campos **o propiedades**, de cualquier visibilidad. |
| La ruta se pasaba en cada llamada. | `.Archivo("ruta.csv")`, una sola vez por entidad. |
| `Read` lanzaba si faltaba el archivo y `ReadOrEmpty` devolvía lista vacía. | `Cargar<T>(exigirArchivo: false)` devuelve tabla vacía; con `true` lanza `FileNotFoundException`. |
| La clase se validaba en cada llamada. | Se valida una vez en `ConstructorModelo.Construir()`. |
| Columnas heredadas detectadas por atributo. | Se declaran explícitamente en la clase derivada. |

`CsvException`, `CsvFormatException` y `CsvConfiguracionException` siguen siendo públicas y no cambiaron.

El formato del archivo (sección 3), el análisis de líneas, la conversión de valores y la escritura atómica con archivo temporal **no cambiaron**.

# 2. Cómo se usa ahora

```csharp
Datos.Modelo.CargarTodo();                         // lee todos los CSV
Datos.Modelo.Tabla<Persona>().Add(new Persona(...));
Datos.Modelo.Guardar<Persona>();                   // reescribe el CSV completo de Persona
```

## 2.1 Reglas para la clase

| # | Regla |
| --- | --- |
| 1 | Cada dato guardado se declara con `.Columna(x => x.Miembro)`; el miembro es un campo o una propiedad legible. |
| 2 | Debe existir un constructor con un parámetro por columna, con el mismo nombre (sin distinguir mayúsculas) y el mismo tipo que el miembro. |
| 3 | Los tipos son los soportados: texto, `char`, `bool`, números, `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan`, `Guid`, enums y sus versiones `?`. |
| 4 | Los nombres de columna no pueden estar vacíos ni contener `,` `"` `\` ni saltos de línea, y no pueden repetirse. |

Una propiedad calculada (por ejemplo `public int Doble => id * 2;`) se podría declarar como columna y se escribiría, pero la clase no tendría un constructor compatible a menos que haya un parámetro `doble`. Conviene declarar solo datos reales.

## 2.2 Escritura y lectura

| Aspecto | Comportamiento |
| --- | --- |
| Archivo existente | Se reemplaza por completo con el contenido de la tabla. |
| Carpeta inexistente | Se crea. |
| Tabla vacía | Se escribe solo el encabezado. |
| Valor `null` | Se escribe la palabra `null` sin comillas. |
| Enum sin `[Flags]` con un valor no definido | `ArgumentException` al guardar. |
| Corte durante el guardado | El archivo anterior queda intacto (archivo temporal + reemplazo). |
| Codificación | UTF-8 sin BOM. |
| Archivo vacío o solo encabezado | Tabla vacía. |
| Orden de las columnas al leer | No importa: se asocian por nombre. |
| Líneas en blanco | Se ignoran. |

`Guardar` escribe **toda** la tabla cada vez.

# 3. El formato del archivo CSV

`CsvManager` usa un formato propio, **inspirado en CSV pero no idéntico al estándar**. Conviene conocerlo para editar archivos a mano o depurar.

## 3.1 Estructura

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

## 3.2 Cómo se escribe cada tipo

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

## 3.3 `null` frente a texto vacío

| En el archivo | Significa |
| --- | --- |
| `null` (sin comillas) | `null` |
| `""` (comillas vacías) | Texto vacío `""` |
| `"null"` (con comillas) | El texto `null` |
| Celda vacía sin comillas (`,,`) | `null` (solo al leer; `Write` nunca la produce) |

Reglas:

- La palabra `null` se reconoce solo en **minúscula** y **sin comillas**. `Null` o `NULL` sin comillas no son `null`: se intentan convertir al tipo del campo y fallan (o, en un `string`, se leen como texto).
- Esta diferencia solo funciona si el archivo **conserva las comillas**. Si una herramienta las quita, `""` se leerá como `null` y `"null"` también.
- La celda vacía sin comillas se sigue aceptando como `null` para que los archivos escritos con versiones anteriores se lean sin cambios. Al volver a guardarlos quedan con la palabra `null`.

## 3.4 Caracteres especiales dentro de textos

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

## 3.5 Diferencias con el CSV estándar

| Aspecto | CSV estándar (RFC 4180) | CsvManager |
| --- | --- | --- |
| Comilla dentro de un texto | Se duplica: `""` | Se escapa: `\"` |
| Saltos de línea en un texto | Permitidos dentro de comillas | Se escriben como `\n` |
| Separador | Varía según región (`,` o `;`) | Siempre `,` |
| Valor nulo | No existe; suele ser una celda vacía | La palabra `null` sin comillas |

Consecuencia: un archivo escrito por otro programa con comillas duplicadas (`""`) **no se lee correctamente**.

## 3.6 Editar a mano

Reglas para no romper el archivo:

1. No cambiar el encabezado, salvo para reordenar columnas (se puede, siempre que estén todas).
2. Poner los textos entre comillas, sobre todo si contienen comas.
3. Usar punto decimal (`3.5`, no `3,5`).
4. Cada fila debe tener exactamente tantas celdas como el encabezado.
5. Respetar los formatos de fecha de la tabla 5.2.
6. Guardar en UTF-8.
7. Escribir los valores ausentes como `null` (minúscula, sin comillas).

---

# 4. Errores

Jerarquía:

```text
Exception
 └── CsvException
      ├── CsvFormatException          ← el ARCHIVO tiene un problema (Linea, Columna)
      └── CsvConfiguracionException   ← la configuración de la entidad está mal
```

Los errores de entrada y salida (`IOException`, `UnauthorizedAccessException`) no se envuelven.

## 4.1 `CsvFormatException` — corregir el archivo

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

## 4.2 `CsvConfiguracionException` — corregir el código

Los mensajes de configuración (columnas, tipos, constructor, archivo duplicado) están en la sección 7 de la documentación del Modelo. Se lanzan al llamar a `Construir()`.

## 4.3 Al escribir

| Excepción | Causa |
| --- | --- |
| `ArgumentException`: `La tabla contiene un elemento null.` | Un elemento `null` (`Tabla<T>` ya los rechaza al agregar). |
| `ArgumentException`: `El valor V no está definido en el enum E...` | Enum sin `[Flags]` con un valor fuera de su lista. |

---

# 5. Funcionamiento interno

```text
CsvManager (internal)
├── Escribir<T>(entidad, datos)     encabezado + una línea por objeto; lock; archivo temporal; File.Move
├── Leer<T>(entidad, exigir)        valida encabezado; BuscarConstructor una vez; una fila = un objeto
├── ValidarConstructor(entidad)     usado por Construir()
├── EsTipoSoportado(tipo)           usado por Construir()
├── BuscarConstructor(tipo, miembros)   empareja parámetros con miembros por nombre y tipo
├── FormatearValor / EscaparString  de valor a texto
└── ParsearLinea / ConvertirValor   de texto a valor
```

Lo que lee y escribe cada columna es un `Miembro` (campo o propiedad obtenido de la lambda): `Miembro.Leer(objeto)` usa `FieldInfo.GetValue` o `PropertyInfo.GetValue`, que funcionan con miembros privados.

La reconstrucción sigue usando el **constructor**, nunca los setters: por eso una propiedad `{ get; }` o `{ get; private set; }` sirve como columna.

El orden de los casos de `FormatearValor`, la lógica de `ParsearLinea` (banderas `escapado`, `dentroDeString`, `huboComillas`) y las reglas de `ConvertirValor` son las mismas que antes.

# 6. Decisiones y limitaciones

| Tema | Detalle |
| --- | --- |
| Un solo `lock` global | Leer un archivo puede esperar a que termine de escribirse otro. El `lock` solo protege dentro del mismo proceso. |
| Archivos grandes | `File.ReadAllLines` carga el archivo entero en memoria. |
| Sin transacciones | Guardar varias tablas no es atómico en conjunto. |
| Formato propio | No es el CSV estándar de la RFC 4180 (ver 3.5). |
| Cambios de estructura | Renombrar o agregar columnas deja inservibles los archivos viejos. |
| Excel | Puede alterar el archivo al guardarlo (separador, comillas, codificación). Preferir un editor de texto. |
| Datos sensibles | El archivo es texto plano, sin cifrado. |
| Sin API pública | `CsvManager` ya no es público. Si se necesita usarlo sin `Modelo`, hay que volver a exponer `Leer` y `Escribir` y construir un `EntidadModelo` (que también es interno). |

# 7. Casos probados

Con el programa de prueba sobre .NET 8: guardado y recarga de texto con comillas escapadas, enteros, `int?` con valor, `decimal` con punto, propiedades `{ get; }` y `{ get; set; }`, campos privados, herencia (campo privado de la base), archivo inexistente (tabla vacía) y ejecución de `GuardarTodo` con cinco entidades.

**No probados en esta versión:** `null` escrito y leído en el CSV, columna con nombre distinto al del miembro, fechas, `Guid`, enums `[Flags]`, `char`, archivos con `;`, comillas sin cerrar, línea terminada en `\`, cantidad de columnas incorrecta y excepciones lanzadas por el constructor. Ese código no se modificó respecto de la versión anterior, pero no se volvió a ejecutar.
