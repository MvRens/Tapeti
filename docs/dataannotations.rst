Validating messages
===================
To validate the contents of messages, Tapeti provides the Tapeti.DataAnnotations package. Once installed and enabled, it verifies each message that is published or consumed using the standard System.ComponentModel.DataAnnotations.

To enable the validation extension, include it in your TapetiConfig:

::

  var config = new TapetiConfig(new SimpleInjectorDependencyResolver(container))
      .WithDataAnnotations()
      .RegisterAllControllers()
      .Build();


Annotations
-----------
All `ValidationAttribute <https://docs.microsoft.com/en-us/dotnet/api/system.componentmodel.dataannotations.validationattribute>`_ derived annotations are supported. For example, use the Required attribute to indicate a field is required:

::

  public class RabbitEscapedMessage
  {
      [Required]
      public string Name { get; set; }

      public string LastKnownHutch { get; set; }
  }

Or the Range attribute to indicate valid ranges:

::

  public class RabbitBirthdayMessage
  {
      [Range(1, 15, ErrorMessage = "Sorry, we have no birthday cards for ages below {1} or above {2}")]
      public int Age { get; set; }
  }

Required GUIDs
--------------
Using the standard validation attributes it is tricky to get a Guid to be required, as it is a struct which defaults to Guid.Empty. Using Nullable<Guid> may work, but then your business logic will look like it is supposed to be optional.

For this reason, the Tapeti.DataAnnotations.Extensions package can be installed from NuGet into your messaging package. It contains the RequiredGuid attribute which specifically checks for Guid.Empty.

::

  public class RabbitBornMessage
  {
      [RequiredGuid]
      public Guid RabbitId { get; set; }
  }