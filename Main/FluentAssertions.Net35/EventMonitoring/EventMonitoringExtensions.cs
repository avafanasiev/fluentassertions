using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using FluentAssertions.Common;

namespace FluentAssertions.EventMonitoring
{
    /// <summary>
    ///   Provides extension methods for monitoring and querying events.
    /// </summary>
    [DebuggerNonUserCode]
    public static class EventMonitoringExtensions
    {
        private const string PropertyChangedEventName = "PropertyChanged";

        private static readonly EventRecordersMap eventRecordersMap = new EventRecordersMap();

#if !SILVERLIGHT
        /// <summary>
        ///   Starts monitoring an object for its events.
        /// </summary>
        /// <exception cref = "ArgumentNullException">Thrown if eventSource is Null.</exception>
        public static IEnumerable<EventRecorder> MonitorEvents(this object eventSource)
        {
            return MonitorEventsRaisedBy(eventSource);
        }
#else
        /// <summary>
        ///   Starts monitoring an object for its <see cref="INotifyPropertyChanged.PropertyChanged"/> events.
        /// </summary>
        /// <exception cref = "ArgumentNullException">Thrown if eventSource is Null.</exception>
        public static IEnumerable<EventRecorder> MonitorEvents(this INotifyPropertyChanged eventSource)
        {
            return MonitorEventsRaisedBy(eventSource);
        }
#endif

        private static IEnumerable<EventRecorder> MonitorEventsRaisedBy(object eventSource)
        {
            if (eventSource == null)
            {
                throw new NullReferenceException("Cannot monitor the events of a <null> object.");
            }

            EventRecorder[] recorders = BuildRecorders(eventSource);

            eventRecordersMap.Add(eventSource, recorders);

            return recorders;
        }

#if !SILVERLIGHT
        private static EventRecorder[] BuildRecorders(object eventSource)
        {
            var recorders =
                eventSource.GetType().GetEvents().Select(@event => CreateEventHandler(eventSource, @event)).ToArray();

            if (!recorders.Any())
            {
                throw new InvalidOperationException(
                    string.Format("Type {0} does not expose any events.", eventSource.GetType().Name));
            }
            
            return recorders;
        }

        private static EventRecorder CreateEventHandler(object eventSource, EventInfo eventInfo)
        {
            var eventRecorder = new EventRecorder(eventSource, eventInfo.Name);

            Delegate handler = EventHandlerFactory.GenerateHandler(eventInfo.EventHandlerType, eventRecorder);
            eventInfo.AddEventHandler(eventSource, handler);

            return eventRecorder;
        }
#else
        private static EventRecorder[] BuildRecorders(object eventSource)
        {
            var eventRecorder = new EventRecorder(eventSource, PropertyChangedEventName);

            ((INotifyPropertyChanged)eventSource).PropertyChanged += (sender, args) => eventRecorder.RecordEvent(sender, args);
            return new[] {eventRecorder};
        }
#endif

#if !SILVERLIGHT
        /// <summary>
        /// Asserts that an object has raised a particular event at least once.
        /// </summary>
        /// <param name="eventName">
        /// The name of the event that should have been raised.
        /// </param>
        /// <remarks>
        /// You must call <see cref="MonitorEvents"/> on the same object prior to this call so that Fluent Assertions can
        /// subscribe for the events of the object.
        /// </remarks>
        public static EventRecorder ShouldRaise(this object eventSource, string eventName)
        {
            return ShouldRaise(eventSource, eventName, "");
        }

        /// <summary>
        /// Asserts that an object has raised a particular event at least once.
        /// </summary>
        /// <param name="eventName">
        /// The name of the event that should have been raised.
        /// </param>
        /// <param name="reason">
        /// A formatted phrase explaining why the assertion should be satisfied. If the phrase does not 
        /// start with the word <i>because</i>, it is prepended to the message.
        /// </param>
        /// <param name="reasonParameters">
        /// Zero or more values to use for filling in any <see cref="string.Format(string,object[])"/> compatible placeholders.
        /// </param>
        /// <remarks>
        /// You must call <see cref="MonitorEvents"/> on the same object prior to this call so that Fluent Assertions can
        /// subscribe for the events of the object.
        /// </remarks>
        public static EventRecorder ShouldRaise(
            this object eventSource, string eventName, string reason, params object[] reasonParameters)
        {
            EventRecorder eventRecorder = GetRecorderForEvent(eventSource, eventName);

            if (!eventRecorder.Any())
            {
                Execute.Fail("Expected object {1} to raise event {0}{2}, but it did not.", eventName, eventSource,
                    reason, reasonParameters);
            }

            return eventRecorder;
        }

        /// <summary>
        /// Asserts that an object has not raised a particular event.
        /// </summary>
        /// <param name="eventName">
        /// The name of the event that should not be raised.
        /// </param>
        /// <remarks>
        /// You must call <see cref="MonitorEvents"/> on the same object prior to this call so that Fluent Assertions can
        /// subscribe for the events of the object.
        /// </remarks>
        public static void ShouldNotRaise(this object eventSource, string eventName)
        {
            ShouldNotRaise(eventSource, eventName, "");
        }

        /// <summary>
        /// Asserts that an object has not raised a particular event.
        /// </summary>
        /// <param name="eventName">
        /// The name of the event that should not be raised.
        /// </param>
        /// <param name="reason">
        /// A formatted phrase explaining why the assertion should be satisfied. If the phrase does not 
        /// start with the word <i>because</i>, it is prepended to the message.
        /// </param>
        /// <param name="reasonParameters">
        /// Zero or more values to use for filling in any <see cref="string.Format(string,object[])"/> compatible placeholders.
        /// </param>
        /// <remarks>
        /// You must call <see cref="MonitorEvents"/> on the same object prior to this call so that Fluent Assertions can
        /// subscribe for the events of the object.
        /// </remarks>
        public static void ShouldNotRaise(
            this object eventSource, string eventName, string reason, params object[] reasonParameters)
        {
            EventRecorder eventRecorder = eventRecordersMap[eventSource].FirstOrDefault(r => r.EventName == eventName);
            if (eventRecorder == null)
            {
                throw new InvalidOperationException(string.Format(
                    "Type <{0}> does not expose an event named \"{1}\".", eventSource.GetType().Name, eventName));
            }

            if (eventRecorder.Any())
            {
                Execute.Fail("Expected object {1} to not raise event {0}{2}, but it did.", eventName, eventSource,
                    reason, reasonParameters);
            }
        }

#endif
        
        /// <summary>
        /// Asserts that an object has raised the <see cref="INotifyPropertyChanged.PropertyChanged"/> event for a particular property.
        /// </summary>
        /// <remarks>
        /// You must call <see cref="MonitorEvents"/> on the same object prior to this call so that Fluent Assertions can
        /// subscribe for the events of the object.
        /// </remarks>
        public static IEventRecorder ShouldRaisePropertyChangeFor<T>(
            this T eventSource, Expression<Func<T, object>> propertyExpression)
        {
            return ShouldRaisePropertyChangeFor(eventSource, propertyExpression, "");
        }

        /// <summary>
        /// Asserts that an object has raised the <see cref="INotifyPropertyChanged.PropertyChanged"/> event for a particular property.
        /// </summary>
        /// <param name="reason">
        /// A formatted phrase explaining why the assertion should be satisfied. If the phrase does not 
        /// start with the word <i>because</i>, it is prepended to the message.
        /// </param>
        /// <param name="reasonParameters">
        /// Zero or more values to use for filling in any <see cref="string.Format(string,object[])"/> compatible placeholders.
        /// </param>
        /// <remarks>
        /// You must call <see cref="MonitorEvents"/> on the same object prior to this call so that Fluent Assertions can
        /// subscribe for the events of the object.
        /// </remarks>
        public static IEventRecorder ShouldRaisePropertyChangeFor<T>(
            this T eventSource, Expression<Func<T, object>> propertyExpression,
            string reason, params object[] reasonParameters)
        {
            EventRecorder eventRecorder = GetRecorderForEvent(eventSource, PropertyChangedEventName);

            if (!eventRecorder.Any())
            {
                Execute.Fail("Expected object {1} to raise event {0}{2}, but it did not.", PropertyChangedEventName, eventSource,
                    reason, reasonParameters);
            }

            return eventRecorder.WithArgs<PropertyChangedEventArgs>(
                    args => args.PropertyName == propertyExpression.GetPropertyInfo().Name);
        }
        
        /// <summary>
        /// Asserts that an object has not raised the <see cref="INotifyPropertyChanged.PropertyChanged"/> event for a particular property.
        /// </summary>
        /// <remarks>
        /// You must call <see cref="MonitorEvents"/> on the same object prior to this call so that Fluent Assertions can
        /// subscribe for the events of the object.
        /// </remarks>
        public static void ShouldNotRaisePropertyChangeFor<T>(
            this T eventSource, Expression<Func<T, object>> propertyExpression)
        {
            ShouldNotRaisePropertyChangeFor(eventSource, propertyExpression, "");
        }

        /// <summary>
        /// Asserts that an object has not raised the <see cref="INotifyPropertyChanged.PropertyChanged"/> event for a particular property.
        /// </summary>
        /// <param name="reason">
        /// A formatted phrase explaining why the assertion should be satisfied. If the phrase does not 
        /// start with the word <i>because</i>, it is prepended to the message.
        /// </param>
        /// <param name="reasonParameters">
        /// Zero or more values to use for filling in any <see cref="string.Format(string,object[])"/> compatible placeholders.
        /// </param>
        /// <remarks>
        /// You must call <see cref="MonitorEvents"/> on the same object prior to this call so that Fluent Assertions can
        /// subscribe for the events of the object.
        /// </remarks>
        public static void ShouldNotRaisePropertyChangeFor<T>(
            this T eventSource, Expression<Func<T, object>> propertyExpression,
            string reason, params object[] reasonParameters)
        {
            EventRecorder eventRecorder = GetRecorderForEvent(eventSource, PropertyChangedEventName);

            string propertyName = propertyExpression.GetPropertyInfo().Name;

            if (eventRecorder.Any(@event => GetAffectedPropertyName(@event) == propertyName))
            {
                Execute.Fail("Did not expect object {1} to raise the \"PropertyChanged\" event for property {0}{2}, but it did.", 
                    propertyName, eventSource,
                    reason, reasonParameters);
            }
        }

        private static EventRecorder GetRecorderForEvent<T>(T eventSource, string eventName)
        {
            EventRecorder eventRecorder = eventRecordersMap[eventSource].FirstOrDefault(r => r.EventName == eventName);
            if (eventRecorder == null)
            {
                throw new InvalidOperationException(string.Format(
                    "Type <{0}> does not expose an event named \"{1}\".", eventSource.GetType().Name, eventName));
            }

            return eventRecorder;
        }

        private static string GetAffectedPropertyName(RecordedEvent @event)
        {
            return @event.Parameters.OfType<PropertyChangedEventArgs>().Single().PropertyName;
        }

        /// <summary>
        /// Asserts that all occurences of the event originated from the <param name="expectedSender"/>.
        /// </summary>
        public static IEventRecorder WithSender(this IEventRecorder eventRecorder, object expectedSender)
        {
            foreach (RecordedEvent recordedEvent in eventRecorder)
            {
                if (recordedEvent.Parameters.Length == 0)
                {
                    throw new ArgumentException(string.Format(
                        "Expected event from sender <{0}>, but event {1} does not include any arguments",
                        expectedSender, eventRecorder.EventName));
                }

                object actualSender = recordedEvent.Parameters.First();
                Execute.Verify(ReferenceEquals(actualSender, expectedSender),
                    "Expected sender {0}, but found {1}.", expectedSender, actualSender, "", null);
            }

            return eventRecorder;
        }

        /// <summary>
        /// Asserts that at least one occurrence of the event had an <see cref="EventArgs"/> object matching a predicate.
        /// </summary>
        public static IEventRecorder WithArgs<T>(this IEventRecorder eventRecorder, Expression<Func<T, bool>> predicate) where T : EventArgs
        {
            Func<T, bool> compiledPredicate = predicate.Compile();

            if (eventRecorder.First().Parameters.OfType<T>().Count() == 0)
            {
                throw new ArgumentException("No argument of event " + eventRecorder.EventName + " is of type <" + typeof(T) + ">.");
            }

            if (!eventRecorder.Any(@event => compiledPredicate(@event.Parameters.OfType<T>().Single())))
            {
                Execute.Fail(
                    "Expected at least one event with arguments matching {0}, but found none.", 
                    predicate.Body, null, "", null);
            }

            return eventRecorder;
        }
    }
}