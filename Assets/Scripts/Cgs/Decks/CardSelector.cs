/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using CardGameView;
using Cgs.Cards;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Cgs.Decks
{
    public class CardSelector : MonoBehaviour
    {
        public DeckEditor editor;
        public SearchResults results;

        private bool _wasDown;
        private bool _wasUp;
        private bool _wasLeft;
        private bool _wasRight;
        private bool _wasPageDown;
        private bool _wasPageUp;
        private bool _wasPageLeft;
        private bool _wasPageRight;

        void Update()
        {
            if (CardGameManager.Instance.ModalCanvas != null || editor.searchResults.inputField.isFocused)
                return;

            if (CardViewer.Instance.Zoom && CardViewer.Instance.ZoomTime > 0.5f && SwipeManager.DetectSwipe())
            {
                if (SwipeManager.IsSwipingUp())
                    SelectEditorDown();
                else if (SwipeManager.IsSwipingDown())
                    SelectEditorUp();
                if (SwipeManager.IsSwipingRight())
                    SelectResultsLeft();
                else if (SwipeManager.IsSwipingLeft())
                    SelectResultsRight();
            }

            if (Input.GetButtonDown(Inputs.Vertical) || Math.Abs(Input.GetAxis(Inputs.Vertical)) > Inputs.Tolerance)
            {
                if (Input.GetAxis(Inputs.Vertical) < 0 && !_wasDown)
                    SelectResultsDown();
                else if (Input.GetAxis(Inputs.Vertical) > 0 && !_wasUp)
                    SelectResultsUp();
            }
            else if (Input.GetButtonDown(Inputs.Horizontal) ||
                     Math.Abs(Input.GetAxis(Inputs.Horizontal)) > Inputs.Tolerance)
            {
                if (Input.GetAxis(Inputs.Horizontal) < 0 && !_wasLeft)
                    SelectResultsLeft();
                else if (Input.GetAxis(Inputs.Horizontal) > 0 && !_wasRight)
                    SelectResultsRight();
            }

            if (Input.GetButtonDown(Inputs.PageVertical) ||
                Math.Abs(Input.GetAxis(Inputs.PageVertical)) > Inputs.Tolerance)
            {
                if (Input.GetAxis(Inputs.PageVertical) < 0 && !_wasPageDown)
                    SelectEditorDown();
                else if (Input.GetAxis(Inputs.PageVertical) > 0 && !_wasPageUp)
                    SelectEditorUp();
            }
            else if ((Input.GetButtonDown(Inputs.PageHorizontal) ||
                      Math.Abs(Input.GetAxis(Inputs.PageHorizontal)) > Inputs.Tolerance))
            {
                if (Input.GetAxis(Inputs.PageHorizontal) < 0 && !_wasPageLeft)
                    SelectEditorLeft();
                else if (Input.GetAxis(Inputs.PageHorizontal) > 0 && !_wasPageRight)
                    SelectEditorRight();
            }

            _wasDown = Input.GetAxis(Inputs.Vertical) < 0;
            _wasUp = Input.GetAxis(Inputs.Vertical) > 0;
            _wasLeft = Input.GetAxis(Inputs.Horizontal) < 0;
            _wasRight = Input.GetAxis(Inputs.Horizontal) > 0;
            _wasPageDown = Input.GetAxis(Inputs.PageVertical) < 0;
            _wasPageUp = Input.GetAxis(Inputs.PageVertical) > 0;
            _wasPageLeft = Input.GetAxis(Inputs.PageHorizontal) < 0;
            _wasPageRight = Input.GetAxis(Inputs.PageHorizontal) > 0;
        }

        public void SelectResultsLeft()
        {
            if (EventSystem.current.alreadySelecting)
                return;

            if (results.layoutArea.childCount < 1)
            {
                EventSystem.current.SetSelectedGameObject(null);
                return;
            }

            for (int i = results.layoutArea.childCount - 1; i >= 0; i--)
            {
                if (results.layoutArea.GetChild(i).GetComponent<CardModel>() != CardViewer.Instance.SelectedCardModel)
                    continue;
                i--;
                if (i < 0)
                {
                    results.DecrementPage();
                    i = results.layoutArea.childCount - 1;
                }

                EventSystem.current.SetSelectedGameObject(results.layoutArea.GetChild(i).gameObject);
                return;
            }

            EventSystem.current.SetSelectedGameObject(results.layoutArea.GetChild(0).gameObject);
            if (CardViewer.Instance != null && CardViewer.Instance.SelectedCardModel != null)
                CardViewer.Instance.IsVisible = true;
        }

        public void SelectResultsRight()
        {
            if (EventSystem.current.alreadySelecting)
                return;

            if (results.layoutArea.childCount < 1)
            {
                EventSystem.current.SetSelectedGameObject(null);
                return;
            }

            for (var i = 0; i < results.layoutArea.childCount; i++)
            {
                if (results.layoutArea.GetChild(i).GetComponent<CardModel>() != CardViewer.Instance.SelectedCardModel)
                    continue;
                i++;
                if (i == results.layoutArea.childCount)
                {
                    results.IncrementPage();
                    i = 0;
                }

                EventSystem.current.SetSelectedGameObject(results.layoutArea.GetChild(i).gameObject);
                return;
            }

            EventSystem.current.SetSelectedGameObject(results.layoutArea.GetChild(0).gameObject);
            if (CardViewer.Instance != null && CardViewer.Instance.SelectedCardModel != null)
                CardViewer.Instance.IsVisible = true;
        }

        public void SelectResultsDown()
        {
            results.IncrementPage();
        }

        public void SelectResultsUp()
        {
            results.DecrementPage();
        }

        public void SelectEditorDown()
        {
            if (EventSystem.current.alreadySelecting)
                return;

            List<CardModel> editorCards = editor.CardModels;
            if (editorCards.Count < 1)
            {
                EventSystem.current.SetSelectedGameObject(null);
                return;
            }

            for (var i = 0; i < editorCards.Count; i++)
            {
                if (editorCards[i] != CardViewer.Instance.SelectedCardModel)
                    continue;
                i++;
                if (i == editorCards.Count)
                    i = 0;
                EventSystem.current.SetSelectedGameObject(editorCards[i].gameObject);
                editor.FocusScrollRectOn(editorCards[i]);
                return;
            }

            EventSystem.current.SetSelectedGameObject(editorCards[0].gameObject);
            editor.FocusScrollRectOn(editorCards[0]);
            if (CardViewer.Instance != null && CardViewer.Instance.SelectedCardModel != null)
                CardViewer.Instance.IsVisible = true;
        }

        public void SelectEditorUp()
        {
            if (EventSystem.current.alreadySelecting)
                return;

            List<CardModel> editorCards = editor.CardModels;
            if (editorCards.Count < 1)
            {
                EventSystem.current.SetSelectedGameObject(null);
                return;
            }

            for (int i = editorCards.Count - 1; i >= 0; i--)
            {
                if (editorCards[i] != CardViewer.Instance.SelectedCardModel)
                    continue;
                i--;
                if (i < 0)
                    i = editorCards.Count - 1;
                EventSystem.current.SetSelectedGameObject(editorCards[i].gameObject);
                editor.FocusScrollRectOn(editorCards[i]);
                return;
            }

            EventSystem.current.SetSelectedGameObject(editorCards[editorCards.Count - 1].gameObject);
            editor.FocusScrollRectOn(editorCards[editorCards.Count - 1]);
            if (CardViewer.Instance != null && CardViewer.Instance.SelectedCardModel != null)
                CardViewer.Instance.IsVisible = true;
        }

        public void SelectEditorLeft()
        {
            if (EventSystem.current.alreadySelecting)
                return;

            List<CardModel> editorCards = editor.CardModels;
            if (editorCards.Count < 1)
            {
                EventSystem.current.SetSelectedGameObject(null);
                return;
            }

            Transform startParent = null;
            for (int i = editorCards.Count - 1; i >= 0; i--)
            {
                if (startParent == null && editorCards[i] != CardViewer.Instance.SelectedCardModel)
                    continue;
                if (editorCards[i] == CardViewer.Instance.SelectedCardModel)
                    startParent = editorCards[i].transform.parent;
                if (startParent == editorCards[i].transform.parent)
                    continue;
                EventSystem.current.SetSelectedGameObject(editorCards[i].gameObject);
                editor.FocusScrollRectOn(editorCards[i]);
                return;
            }

            editor.scrollRect.horizontalNormalizedPosition = 1;
            EventSystem.current.SetSelectedGameObject(editorCards[editorCards.Count - 1].gameObject);
            if (CardViewer.Instance != null && CardViewer.Instance.SelectedCardModel != null)
                CardViewer.Instance.IsVisible = true;
        }

        public void SelectEditorRight()
        {
            if (EventSystem.current.alreadySelecting)
                return;

            List<CardModel> editorCards = editor.CardModels;
            if (editorCards.Count < 1)
            {
                EventSystem.current.SetSelectedGameObject(null);
                return;
            }

            Transform startParent = null;
            foreach (CardModel cardModel in editorCards.Where(t =>
                startParent != null || t == CardViewer.Instance.SelectedCardModel))
            {
                if (cardModel == CardViewer.Instance.SelectedCardModel)
                    startParent = cardModel.transform.parent;
                if (startParent == cardModel.transform.parent)
                    continue;
                EventSystem.current.SetSelectedGameObject(cardModel.gameObject);
                editor.FocusScrollRectOn(cardModel);
                return;
            }

            editor.scrollRect.horizontalNormalizedPosition = 0;
            EventSystem.current.SetSelectedGameObject(editorCards[0].gameObject);
            if (CardViewer.Instance != null && CardViewer.Instance.SelectedCardModel != null)
                CardViewer.Instance.IsVisible = true;
        }

        [UsedImplicitly]
        public void DecrementPage()
        {
            var rectTransform = GetComponent<RectTransform>();
            if (rectTransform.rect.width > rectTransform.rect.height) // Landscape
                SelectEditorLeft();
            else // Portrait
                results.DecrementPage();
        }

        [UsedImplicitly]
        public void IncrementPage()
        {
            var rectTransform = GetComponent<RectTransform>();
            if (rectTransform.rect.width > rectTransform.rect.height) // Landscape
                SelectEditorRight();
            else // Portrait
                results.IncrementPage();
        }

        [UsedImplicitly]
        public void NavigateLeft()
        {
            var rectTransform = GetComponent<RectTransform>();
            if (rectTransform.rect.width < rectTransform.rect.height) // Portrait
                SelectResultsLeft();
            else // Landscape
                results.DecrementPage();
        }

        [UsedImplicitly]
        public void NavigateRight()
        {
            var rectTransform = GetComponent<RectTransform>();
            if (rectTransform.rect.width < rectTransform.rect.height) // Portrait
                SelectResultsRight();
            else // Landscape
                results.IncrementPage();
        }
    }
}
