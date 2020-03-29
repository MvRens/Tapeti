Tapeti.Cmd
==========

The Tapeti command-line tool provides various operations for managing messages. It tries to be compatible with all type of messages, but has been tested only against JSON messages, specifically those sent by Tapeti.


Common parameters
-----------------

Most operations support the following parameters. All are optional.

-h <hostname>, --host <hostname>
  Specifies the hostname of the RabbitMQ server. Default is localhost.

--port <port>
  Specifies the AMQP port of the RabbitMQ server. Default is 5672.

-v <virtualhost>, --virtualhost <virtualhost>
  Specifies the virtual host to use. Default is /.

-u <username>, --username <username>
  Specifies the username to authenticate the connection. Default is guest.

-p <password>, --password <username>
  Specifies the password to authenticate the connection. Default is guest.


Example:
::

  .\Tapeti.Cmd.exe <operation> -h rabbitmq-server -u tapeti -p topsecret



Export
------

Fetches messages from a queue and writes them to disk.

-q <queue>, --queue <queue>
  *Required*. The queue to read the messages from.

-o <target>, --output <target>
  *Required*. Path or filename (depending on the chosen serialization method) where the messages will be output to.

-r, --remove
  If specified messages are acknowledged and removed from the queue. If not messages are kept.

-n <count>, --maxcount <count>
  Maximum number of messages to retrieve from the queue. If not specified all messages are exported.

-s <method>, --serialization <method>
  The method used to serialize the message for import or export. Valid options: SingleFileJSON, EasyNetQHosepipe. Defaults to SingleFileJSON. See Serialization methods below for more information.


Example:
::

  .\Tapeti.Cmd.exe export -q tapeti.example.01 -o dump.json



Import
------

Read messages from disk as previously exported and publish them to a queue.

-i <source>, --input <source>
  Path or filename (depending on the chosen serialization method) where the messages will be read from.

-m <message>, --message <message>
  Single message to be sent, in the same format as used for SingleFileJSON. Serialization argument has no effect when using this input. Be sure to quote the entire message, and escape quotes within the message with another quote.

-c, --pipe
  Messages are read from the standard input pipe, in the same format as used for SingleFileJSON. Serialization argument has no effect when using  this input.

-e, --exchange
  If specified publishes to the originating exchange using the original routing key. By default these are ignored and the message is published directly to the originating queue.

-s <method>, --serialization <method>
  The method used to serialize the message for import or export. Valid options: SingleFileJSON, EasyNetQHosepipe. Defaults to SingleFileJSON. See Serialization methods below for more information.

Either input, message or pipe is required.

Example:
::

  .\Tapeti.Cmd.exe import -i dump.json



Shovel
------

Reads messages from a queue and publishes them to another queue, optionally to another RabbitMQ server.

-q <queue>, --queue <queue>
  *Required*. The queue to read the messages from.

-t <queue>, --targetqueue <queue>
  The target queue to publish the messages to. Defaults to the source queue if a different target host, port or virtualhost is specified. Otherwise it must be different from the source queue.

-r, --remove
  If specified messages are acknowledged and removed from the queue. If not messages are kept.

-n <count>, --maxcount <count>
  Maximum number of messages to retrieve from the queue. If not specified all messages are exported.

--targethost <host>
  Hostname of the target RabbitMQ server. Defaults to the source host. Note that you may still specify a different targetusername for example.

--targetport <port>
  AMQP port of the target RabbitMQ server. Defaults to the source port.

--targetvirtualhost <virtualhost>
  Virtual host used for the target RabbitMQ connection. Defaults to the source virtualhost.

--targetusername <username>
  Username used to connect to the target RabbitMQ server. Defaults to the source username.

--targetpassword <password>
  Password used to connect to the target RabbitMQ server. Defaults to the source password.



Example:
::

  .\Tapeti.Cmd.exe shovel -q tapeti.example.01 -t tapeti.example.06


Serialization methods
---------------------

For importing and exporting messages, Tapeti.Cmd supports two serialization methods.

SingleFileJSON
''''''''''''''
The default serialization method. All messages are contained in a single file, where each line is a JSON document describing the message properties and it's content.

An example message (formatted as multi-line to be more readable, but keep in mind that it must be a single line in the export file to be imported properly):

::

  {
    "DeliveryTag": 1,
    "Redelivered": true,
    "Exchange": "tapeti",
    "RoutingKey": "quote.request",
    "Queue": "tapeti.example.01",
    "Properties": {
      "AppId": null,
      "ClusterId": null,
      "ContentEncoding": null,
      "ContentType": "application/json",
      "CorrelationId": null,
      "DeliveryMode": 2,
      "Expiration": null,
      "Headers": {
        "classType": "Messaging.TapetiExample.QuoteRequestMessage:Messaging.TapetiExample"
      },
      "MessageId": null,
      "Priority": null,
      "ReplyTo": null,
      "Timestamp": 1581600132,
      "Type": null,
      "UserId": null
    },
    "Body": {
      "Amount": 2
    },
    "RawBody": "<JSON encoded byte array>"
  }

The properties correspond to the RabbitMQ client's IBasicProperties and can be omitted if empty.

Either Body or RawBody is present. Body is used if the ContentType is set to application/json, and will contain the original message as an inline JSON object for easy manipulation. For other content types, the RawBody contains the original encoded body.


EasyNetQHosepipe
''''''''''''''''
Provides compatibility with the EasyNetQ Hosepipe's dump/insert format. The source or target parameter must be a path. Each message consists of 3 files, ending in .message.txt, .properties.txt and .info.txt.

As this is only provided for emergency situations, see the source code if you want to know more about the format specification.



Generating an example
---------------------

The "example" operation is available to generate an example message in SingleFileJSON format.

::

  .\Tapeti.Cmd.exe example


To save the output to a file:

::

  .\Tapeti.Cmd.exe example > example.json