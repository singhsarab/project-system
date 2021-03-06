﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.ProjectSystem.VS.UI;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using Moq;
using Xunit;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Editor.Listeners
{
    [ProjectSystemTrait]
    public class TempFileBufferStateListenerTests
    {
        [Fact]
        public void TempFileBufferStateListener_NullEditorState_Throws()
        {
            Assert.Throws<ArgumentNullException>("editorState", () => new TempFileBufferStateListener(null,
                IVsEditorAdaptersFactoryServiceFactory.Create(),
                ITextDocumentFactoryServiceFactory.Create(),
                IProjectThreadingServiceFactory.Create(),
                IVsShellUtilitiesHelperFactory.Create(),
                IServiceProviderFactory.Create()));
        }

        [Fact]
        public void TempFileBufferStateListener_NullEditorAdaptersFactory_Throws()
        {
            Assert.Throws<ArgumentNullException>("editorAdaptersService", () => new TempFileBufferStateListener(
                IProjectFileEditorPresenterFactory.Create(),
                null,
                ITextDocumentFactoryServiceFactory.Create(),
                IProjectThreadingServiceFactory.Create(),
                IVsShellUtilitiesHelperFactory.Create(),
                IServiceProviderFactory.Create()));
        }

        [Fact]
        public void TempFileBufferStateListener_NullTextDocumentFactoryService_Throws()
        {
            Assert.Throws<ArgumentNullException>("textDocumentFactoryService", () => new TempFileBufferStateListener(
                IProjectFileEditorPresenterFactory.Create(),
                IVsEditorAdaptersFactoryServiceFactory.Create(),
                null,
                IProjectThreadingServiceFactory.Create(),
                IVsShellUtilitiesHelperFactory.Create(),
                IServiceProviderFactory.Create()));
        }

        [Fact]
        public void TempFileBufferStateListener_NullThreadingService_Throws()
        {
            Assert.Throws<ArgumentNullException>("threadingService", () => new TempFileBufferStateListener(
                IProjectFileEditorPresenterFactory.Create(),
                IVsEditorAdaptersFactoryServiceFactory.Create(),
                ITextDocumentFactoryServiceFactory.Create(),
                null,
                IVsShellUtilitiesHelperFactory.Create(),
                IServiceProviderFactory.Create()));
        }

        [Fact]
        public void TempFileBufferStateListener_NullShellUtilities_Throws()
        {
            Assert.Throws<ArgumentNullException>("shellUtilities", () => new TempFileBufferStateListener(
                IProjectFileEditorPresenterFactory.Create(),
                IVsEditorAdaptersFactoryServiceFactory.Create(),
                ITextDocumentFactoryServiceFactory.Create(),
                IProjectThreadingServiceFactory.Create(),
                null,
                IServiceProviderFactory.Create()));
        }

        [Fact]
        public async Task TempFileBufferStateListener_Initialize_SubscribesToTextChangedEvents()
        {
            var tempFile = @"C:\Temp\ConsoleApp1.csproj";
            var docData = IVsPersistDocDataFactory.ImplementAsIVsTextBuffer();
            var shellUtilities = IVsShellUtilitiesHelperFactory.ImplementGetRDTInfo(tempFile, docData);
            var textBuffer = ITextBufferFactory.Create();
            var editorAdaptersService = IVsEditorAdaptersFactoryServiceFactory.ImplementGetDocumentBuffer(textBuffer);
            var textDoc = ITextDocumentFactory.Create();
            var textDocFactoryService = ITextDocumentFactoryServiceFactory.ImplementGetTextDocument(textDoc, true);
            var editorModel = IProjectFileEditorPresenterFactory.Create();

            var watcher = new TempFileBufferStateListener(editorModel, editorAdaptersService, textDocFactoryService, new IProjectThreadingServiceMock(), shellUtilities,
                IServiceProviderFactory.Create());

            await watcher.InitializeListenerAsync(tempFile);
            Mock.Get(shellUtilities).Verify(u => u.GetRDTDocumentInfoAsync(It.IsAny<IServiceProvider>(), tempFile), Times.Once);
            Mock.Get(editorAdaptersService).Verify(e => e.GetDocumentBuffer((IVsTextBuffer)docData), Times.Once);
            Mock.Get(textDocFactoryService).Verify(t => t.TryGetTextDocument(textBuffer, out textDoc), Times.Once);

            Mock.Get(textDoc).Raise(t => t.FileActionOccurred += null,
                new[] { null, new TextDocumentFileActionEventArgs(tempFile, DateTime.Now, FileActionTypes.ContentSavedToDisk) });
            Mock.Get(editorModel).Verify(e => e.SaveProjectFileAsync(), Times.Once);
        }

        [Fact]
        public async Task TempFileBufferStateListener_Dispose_UnsubscribtesFromTextChangedEvents()
        {
            var tempFile = @"C:\Temp\ConsoleApp1.csproj";
            var docData = IVsPersistDocDataFactory.ImplementAsIVsTextBuffer();
            var textBuffer = ITextBufferFactory.Create();
            var shellUtilities = IVsShellUtilitiesHelperFactory.ImplementGetRDTInfo(tempFile, docData);
            var editorAdaptersService = IVsEditorAdaptersFactoryServiceFactory.ImplementGetDocumentBuffer(textBuffer);
            var textDoc = ITextDocumentFactory.Create();
            var textDocFactoryService = ITextDocumentFactoryServiceFactory.ImplementGetTextDocument(textDoc, true);
            var editorModel = IProjectFileEditorPresenterFactory.Create();

            var watcher = new TempFileBufferStateListener(editorModel, editorAdaptersService, textDocFactoryService, new IProjectThreadingServiceMock(), shellUtilities,
                IServiceProviderFactory.Create());

            await watcher.InitializeListenerAsync(tempFile);
            Mock.Get(shellUtilities).Verify(u => u.GetRDTDocumentInfoAsync(It.IsAny<IServiceProvider>(), tempFile), Times.Once);
            Mock.Get(editorAdaptersService).Verify(e => e.GetDocumentBuffer((IVsTextBuffer)docData), Times.Once);
            Mock.Get(textDocFactoryService).Verify(t => t.TryGetTextDocument(textBuffer, out textDoc), Times.Once);

            Mock.Get(textDoc).Raise(t => t.FileActionOccurred += null,
                new[] { null, new TextDocumentFileActionEventArgs(tempFile, DateTime.Now, FileActionTypes.ContentSavedToDisk) });
            Mock.Get(editorModel).Verify(e => e.SaveProjectFileAsync(), Times.Once);

            await watcher.DisposeAsync();

            Mock.Get(textDoc).Raise(t => t.FileActionOccurred += null,
                new[] { null, new TextDocumentFileActionEventArgs(tempFile, DateTime.Now, FileActionTypes.ContentSavedToDisk) });
            Mock.Get(editorModel).Verify(e => e.SaveProjectFileAsync(), Times.Once);
        }

        [Theory]
        [InlineData(FileActionTypes.ContentLoadedFromDisk)]
        [InlineData(FileActionTypes.DocumentRenamed)]
        public async Task TempFileBufferStateListener_NonFileSavedToDisk_DoesNotSaveProjectFile(FileActionTypes action)
        {
            var tempFile = @"C:\Temp\ConsoleApp1.csproj";
            var docData = IVsPersistDocDataFactory.ImplementAsIVsTextBuffer();
            var shellUtilities = IVsShellUtilitiesHelperFactory.ImplementGetRDTInfo(tempFile, docData);
            var textBuffer = ITextBufferFactory.Create();
            var editorAdaptersService = IVsEditorAdaptersFactoryServiceFactory.ImplementGetDocumentBuffer(textBuffer);
            var textDoc = ITextDocumentFactory.Create();
            var textDocFactoryService = ITextDocumentFactoryServiceFactory.ImplementGetTextDocument(textDoc, true);
            var editorModel = IProjectFileEditorPresenterFactory.Create();

            var watcher = new TempFileBufferStateListener(editorModel, editorAdaptersService, textDocFactoryService, new IProjectThreadingServiceMock(), shellUtilities,
                IServiceProviderFactory.Create());

            await watcher.InitializeListenerAsync(tempFile);
            Mock.Get(shellUtilities).Verify(u => u.GetRDTDocumentInfoAsync(It.IsAny<IServiceProvider>(), tempFile), Times.Once);
            Mock.Get(editorAdaptersService).Verify(e => e.GetDocumentBuffer((IVsTextBuffer)docData), Times.Once);
            Mock.Get(textDocFactoryService).Verify(t => t.TryGetTextDocument(textBuffer, out textDoc), Times.Once);

            Mock.Get(textDoc).Raise(t => t.FileActionOccurred += null, new[] { null, new TextDocumentFileActionEventArgs(tempFile, DateTime.Now, action) });
            Mock.Get(editorModel).Verify(e => e.SaveProjectFileAsync(), Times.Never);
        }
    }
}
