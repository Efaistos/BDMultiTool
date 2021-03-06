﻿using BDMultiTool.Persistence;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace BDMultiTool.Macros {
    public class MacroManager {
        private static volatile ConcurrentDictionary<String, CycleMacro> macros;
        private MacroGallery macroGallery;
        private MacroAddControl macroAddControl;
        private MovableUserControl ownParentWindow;
        private MovableUserControl macroCreateWindow;

        public MacroManager() {
            macros = new ConcurrentDictionary<String, CycleMacro>();
            macroGallery = new MacroGallery();
            macroGallery.initialize();

            macroAddControl = new MacroAddControl();

            ownParentWindow = App.overlay.addWindowToGrid(macroGallery, "Macros", false);
            macroCreateWindow = App.overlay.addWindowToGrid(macroAddControl, "Create new macro", false);
            App.overlay.addMenuItemToMenu("pack://application:,,,/Resources/macroMenuIcon.png", "Macros").Click += macroMenu_Click;

            PersistenceContainer[] savedMacros = PersistenceUnitThread.persistenceUnit.loadContainersByType(typeof(CycleMacro).Name);
            foreach(PersistenceContainer currentPersistenceContainer in savedMacros) {
                CycleMacro newMacro = new CycleMacro();
                newMacro.updateCycleMacroByPersistenceContainer(currentPersistenceContainer);
                macros.GetOrAdd(newMacro.name, newMacro);
            }
        }

        private void macroMenu_Click(object sender, RoutedEventArgs e) {
            showMacroMenu();
        }

        public void showCreateMacroMenu() {
            macroCreateWindow.Dispatcher.Invoke((Action)(() => {
                macroCreateWindow.Visibility = Visibility.Visible;
            }));
        }

        public void hideCreateMacroMenu() {
            macroCreateWindow.Dispatcher.Invoke((Action)(() => {
                macroCreateWindow.Visibility = Visibility.Hidden;
            }));
        }

        public void showMacroMenu() {
            ownParentWindow.Dispatcher.Invoke((Action)(() => {
                ownParentWindow.Visibility = Visibility.Visible;
            }));
        }

        public void hideMacroMenu() {
            ownParentWindow.Dispatcher.Invoke((Action)(() => {
                ownParentWindow.Visibility = Visibility.Hidden;
            }));
        }

        public void addMacro(CycleMacro macroToAdd) {
            macroToAdd.pause();
            macros.GetOrAdd(macroToAdd.name, macroToAdd);
            macroToAdd.persist();
        }

        public void update() {
            int tempCount = 0;
            ObservableCollection<MacroItemModel> macroItemModels = new ObservableCollection<MacroItemModel>();
            foreach (KeyValuePair<String, CycleMacro> currentMacroKeyValuePair in macros) {
                tempCount++;
                if (currentMacroKeyValuePair.Value.isReady()) {
                    sendMultipleKeys(currentMacroKeyValuePair.Value.getKeys());
                    currentMacroKeyValuePair.Value.reset();
                    currentMacroKeyValuePair.Value.start();
                    Thread.Sleep(20);
                }
                if (currentMacroKeyValuePair.Value.lifeTimeOver()) {
                    removeMacro(currentMacroKeyValuePair.Value);
                } else {
                    MacroItemModel currentMacroItemModel = new MacroItemModel();
                    currentMacroItemModel.macroName = currentMacroKeyValuePair.Value.name;
                    currentMacroItemModel.coolDownTime = currentMacroKeyValuePair.Value.getRemainingCoolDownFormatted();
                    currentMacroItemModel.coolDownPercentage = currentMacroKeyValuePair.Value.getCoolDownPercentage();
                    currentMacroItemModel.lifeTime = currentMacroKeyValuePair.Value.getRemainingLifeTimeFormatted();
                    currentMacroItemModel.lifeTimePercentage = currentMacroKeyValuePair.Value.getLifeTimePercentage();
                    currentMacroItemModel.keyString = currentMacroKeyValuePair.Value.getKeyString();
                    currentMacroItemModel.AddMode = false;

                    if(currentMacroKeyValuePair.Value.paused) {
                        currentMacroItemModel.Paused = true;
                        currentMacroItemModel.NotPaused = false;
                    } else {
                        currentMacroItemModel.Paused = false;
                        currentMacroItemModel.NotPaused = true;
                    }

                    macroItemModels.Add(currentMacroItemModel);

                    macroGallery.Dispatcher.Invoke((Action)(() => {
                        bool macroContained = false;
                        foreach(MacroItemModel currentInnerMacroItemModel in macroGallery.macroItemModels) {
                            if(currentInnerMacroItemModel.macroName == currentMacroItemModel.macroName) {
                                currentInnerMacroItemModel.coolDownPercentage = currentMacroItemModel.coolDownPercentage;
                                currentInnerMacroItemModel.coolDownTime = currentMacroItemModel.coolDownTime;
                                currentInnerMacroItemModel.lifeTime = currentMacroItemModel.lifeTime;
                                currentInnerMacroItemModel.lifeTimePercentage = currentMacroItemModel.lifeTimePercentage;
                                macroContained = true;
                                break;
                            }
                        }
                        if(!macroContained) {
                            macroGallery.addMacro(currentMacroItemModel);
                        }
                    }));
                }
            }


        }

        public void removeMacro(CycleMacro macro) {
            CycleMacro deletedMacro;
            while (!macros.TryRemove(macro.name, out deletedMacro)) { }

            deletedMacro.deletePersistence();

            macroGallery.Dispatcher.Invoke((Action)(() => {
                foreach (MacroItemModel currentInnerMacroItemModel in macroGallery.macroItemModels) {
                    if (currentInnerMacroItemModel.macroName == deletedMacro.name) {
                        macroGallery.macroItemModels.Remove(currentInnerMacroItemModel);
                        break;
                    }
                }
            }));

        }

        public void removeMacroByName(String name) {
            CycleMacro deletedMacro;
            while (!macros.TryRemove(name, out deletedMacro)) { }

            deletedMacro.deletePersistence();

            macroGallery.Dispatcher.Invoke((Action)(() => {
                foreach (MacroItemModel currentInnerMacroItemModel in macroGallery.macroItemModels) {
                    if (currentInnerMacroItemModel.macroName == deletedMacro.name) {
                        macroGallery.macroItemModels.Remove(currentInnerMacroItemModel);
                        break;
                    }
                }
            }));
        }

        public CycleMacro getMacroByName(String name) {
            if(macros.ContainsKey(name)) {
                CycleMacro receivedMacro;

                while (!macros.TryGetValue(name, out receivedMacro)) { }
                return receivedMacro;
            } else {
                return null;
            }
        }

        private void sendMultipleKeys(System.Windows.Forms.Keys[] keys) {
            foreach(System.Windows.Forms.Keys currentKey in keys) {
                App.windowAttacher.sendKeypress(currentKey);
                Thread.Sleep(10);
            }
        }

        public KeyValuePair<String, CycleMacro>[] getActiveMacros() {
            return macros.ToArray();
        }

    }
}
